using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Condominium.Infrastructure;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>As rotas do financeiro do síndico (despesas e lançamentos do mês), de ponta a ponta.</summary>
[Collection(ApiCollection.Name)]
public sealed class FinanceEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Groups = "/api/finance/expense-groups";
    private const string DefaultGroup = "/api/finance/expense-groups/default";
    private const string Expenses = "/api/finance/expenses";
    private const string Months = "/api/finance/months";
    private const string Entries = "/api/finance/entries";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", Groups },
        { "POST", Groups },
        { "POST", DefaultGroup },
        { "PUT", $"{Groups}/{Guid.Empty}" },
        { "GET", Expenses },
        { "POST", Expenses },
        { "PUT", $"{Expenses}/{Guid.Empty}" },
        { "POST", $"{Expenses}/{Guid.Empty}/amount" },
        { "PUT", $"{Expenses}/{Guid.Empty}/installments/1" },
        { "POST", $"{Expenses}/{Guid.Empty}/end" },
        { "GET", $"{Months}/2026-11" },
        { "POST", $"{Months}/2026-11/open" },
        { "POST", $"{Months}/2026-11/close" },
        { "POST", $"{Months}/2026-11/reopen" },
        { "PUT", $"{Entries}/{Guid.Empty}/amount" },
        { "PUT", $"{Entries}/{Guid.Empty}/status" },
    };

    // ---- Acesso ----

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Every_route_requires_a_token(string method, string route)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, null, method == "GET" ? null : new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Only_the_manager_gets_in_a_resident_and_the_super_admin_are_refused(UserRole role)
    {
        await MigrateAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: role);
        string token = await _client.SignInAsync(user);

        foreach ((string method, string route) in new[] { ("GET", Groups), ("POST", Groups), ("GET", Expenses), ("POST", Expenses), ("GET", $"{Months}/{Month(0)}"), ("POST", $"{Months}/{Month(0)}/open"), ("POST", $"{Months}/{Month(0)}/close"), ("POST", $"{Months}/{Month(0)}/reopen") })
        {
            HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, token, method == "GET" ? null : new JsonObject());

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");
            (await response.CodeAsync()).Should().Be("identity.user.not_manager");
        }
    }

    [Fact]
    public async Task A_manager_without_a_condominium_gets_404()
    {
        await MigrateAsync();
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage groups = await _client.SendJsonAsync(HttpMethod.Get, Groups, token);
        HttpResponseMessage month = await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{Month(0)}", token);

        groups.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await groups.CodeAsync()).Should().Be("finance.condominium_not_found");
        month.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Grupos ----

    [Fact]
    public async Task Post_creates_the_group_and_answers_201_with_the_location()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Groups, manager.AccessToken, new JsonObject { ["name"] = "Obras" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.Headers.Location!.ToString().Should().EndWith($"{Groups}/{body.GetProperty("id").GetGuid()}");
        body.GetProperty("name").GetString().Should().Be("Obras");
        body.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("", "expense_group.name.invalid")]
    [InlineData("   ", "expense_group.name.invalid")]
    [InlineData("Um nome de grupo comprido demais para caber nos sessenta caracteres", "expense_group.name.invalid")]
    public async Task Post_with_an_invalid_name_answers_400(string name, string code)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Groups, manager.AccessToken, new JsonObject { ["name"] = name });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Post_of_a_repeated_name_is_409_ignoring_the_case()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateGroupAsync(manager, "Obras");

        HttpResponseMessage repeated = await _client.SendJsonAsync(HttpMethod.Post, Groups, manager.AccessToken, new JsonObject { ["name"] = "OBRAS" });

        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repeated.CodeAsync()).Should().Be("expense_group.name.already_exists");
    }

    [Fact]
    public async Task The_default_group_is_created_once_and_answers_200_every_time()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage first = await _client.SendJsonAsync(HttpMethod.Post, DefaultGroup, manager.AccessToken, new JsonObject());
        HttpResponseMessage second = await _client.SendJsonAsync(HttpMethod.Post, DefaultGroup, manager.AccessToken, new JsonObject());

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement one = await first.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement two = await second.Content.ReadFromJsonAsync<JsonElement>();
        one.GetProperty("name").GetString().Should().Be("Despesas ordinárias");
        two.GetProperty("id").GetGuid().Should().Be(one.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_lists_the_groups_in_alphabetical_order_and_never_caches()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateGroupAsync(manager, "Obras");
        await CreateGroupAsync(manager, "Despesas ordinárias");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Groups, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().Select(group => group.GetProperty("name").GetString()).Should().Equal("Despesas ordinárias", "Obras");
    }

    [Fact]
    public async Task Put_renames_the_group_and_a_stale_or_missing_version_is_refused()
    {
        ManagerSession manager = await RegisterAsync();
        JsonElement group = await CreateGroupJsonAsync(manager, "Obras");
        string route = $"{Groups}/{group.GetProperty("id").GetGuid()}";
        string version = group.GetProperty("version").GetString()!;

        HttpResponseMessage renamed = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["name"] = "Reformas" });
        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["name"] = "Outro" });
        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["name"] = "Outro" });

        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await renamed.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Reformas");
        body.GetProperty("version").GetString().Should().NotBe(version);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.CodeAsync()).Should().Be("finance.version.required");
    }

    [Fact]
    public async Task Put_of_an_unknown_group_is_404()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Groups}/{Guid.NewGuid()}", manager.AccessToken, new JsonObject { ["version"] = "1", ["name"] = "Obras" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("expense_group.not_found");
    }

    // ---- Despesas: cadastro ----

    [Fact]
    public async Task Post_creates_a_single_expense_and_answers_201_with_the_location_and_the_amount()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Obras");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post, Expenses, manager.AccessToken, ExpenseBody("single", group, "Pintura da fachada", Month(0), amount: 1500.50m, supplier: "Tintas Sul"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        response.Headers.Location!.ToString().Should().EndWith($"{Expenses}/{body.GetProperty("id").GetGuid()}");
        body.GetProperty("kind").GetString().Should().Be("single");
        body.GetProperty("groupId").GetGuid().Should().Be(group);
        body.GetProperty("description").GetString().Should().Be("Pintura da fachada");
        body.GetProperty("supplier").GetString().Should().Be("Tintas Sul");
        body.GetProperty("startMonth").GetString().Should().Be(Month(0));
        body.GetProperty("currentAmount").GetDecimal().Should().Be(1500.50m);
        body.GetProperty("dueDay").GetInt32().Should().Be(10);
        body.GetProperty("isEnded").GetBoolean().Should().BeFalse();
        body.GetProperty("installments").GetArrayLength().Should().Be(0);
        body.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_creates_a_fixed_expense_with_an_open_end()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, ExpenseBody("fixed", group, "Portaria", Month(0), amount: 4200m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("kind").GetString().Should().Be("fixed");
        body.GetProperty("currentAmount").GetDecimal().Should().Be(4200m);
        body.GetProperty("endMonth").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement initial = body.GetProperty("amountChanges").EnumerateArray().Single();
        initial.GetProperty("fromMonth").GetString().Should().Be(Month(0));
        initial.GetProperty("amount").GetDecimal().Should().Be(4200m);
    }

    [Fact]
    public async Task Post_creates_a_variable_expense_without_an_amount()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Consumo");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, ExpenseBody("variable", group, "Água", Month(0)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("kind").GetString().Should().Be("variable");
        body.GetProperty("currentAmount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Post_creates_an_installment_expense_splitting_the_total_with_the_centavos_on_the_last_parcel()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Obras");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post, Expenses, manager.AccessToken, ExpenseBody("installment", group, "Elevador", Month(0), amount: 1000m, installmentCount: 6, currentInstallment: 4));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("kind").GetString().Should().Be("installment");
        body.GetProperty("installmentCount").GetInt32().Should().Be(6);
        body.GetProperty("currentInstallment").GetInt32().Should().Be(4);
        JsonElement[] parcels = [.. body.GetProperty("installments").EnumerateArray()];
        parcels.Select(parcel => parcel.GetProperty("number").GetInt32()).Should().Equal(1, 2, 3, 4, 5, 6);
        parcels[0].GetProperty("month").GetString().Should().Be(Month(-3));
        parcels[3].GetProperty("month").GetString().Should().Be(Month(0));
        parcels[5].GetProperty("month").GetString().Should().Be(Month(2));
        parcels.Sum(parcel => parcel.GetProperty("amount").GetDecimal()).Should().Be(1000m);
        parcels[0].GetProperty("amount").GetDecimal().Should().Be(166.66m);
        parcels[5].GetProperty("amount").GetDecimal().Should().Be(166.70m);
        body.GetProperty("lastMonth").GetString().Should().Be(Month(2));
    }

    [Theory]
    [InlineData("castelo")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Post_with_an_unknown_kind_answers_400(string? kind)
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Obras");
        JsonObject body = ExpenseBody("single", group, "Pintura", Month(0), amount: 10m);
        body["kind"] = kind;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("expense.kind.invalid");
    }

    [Theory]
    [InlineData("description", "", "expense.description.invalid")]
    [InlineData("supplier", "Fornecedor com um nome muito comprido que passa de cento e vinte caracteres, daqueles que ninguém digita de propósito na tela", "expense.supplier.invalid")]
    [InlineData("amount", 0, "expense.amount.invalid")]
    [InlineData("amount", -5, "expense.amount.invalid")]
    [InlineData("amount", 10.123, "expense.amount.invalid")]
    [InlineData("dueDay", null, "expense.due_day.invalid")]
    [InlineData("dueDay", 0, "expense.due_day.invalid")]
    [InlineData("dueDay", 32, "expense.due_day.invalid")]
    [InlineData("startMonth", "2026-13", "month.invalid")]
    [InlineData("startMonth", "novembro", "month.invalid")]
    public async Task Post_with_an_invalid_field_answers_400_with_the_code_of_the_field(string field, object? value, string code)
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Obras");
        JsonObject body = ExpenseBody("single", group, "Pintura", Month(0), amount: 10m);
        body[field] = JsonSerializer.SerializeToNode(value);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Theory]
    [InlineData(1, 1, "expense.installment_count.invalid")]
    [InlineData(121, 1, "expense.installment_count.invalid")]
    [InlineData(6, 7, "expense.current_installment.invalid")]
    [InlineData(6, 0, "expense.current_installment.invalid")]
    public async Task Post_of_an_installment_expense_with_bad_parcels_answers_400(int count, int current, string code)
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Obras");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post, Expenses, manager.AccessToken, ExpenseBody("installment", group, "Elevador", Month(0), amount: 1000m, installmentCount: count, currentInstallment: current));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Post_of_a_fixed_expense_ending_before_it_starts_answers_400()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        JsonObject body = ExpenseBody("fixed", group, "Portaria", Month(0), amount: 10m);
        body["endMonth"] = Month(-1);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("expense.end_before_start");
    }

    [Fact]
    public async Task Post_in_a_group_that_does_not_exist_or_belongs_to_another_condominium_is_404()
    {
        ManagerSession manager = await RegisterAsync();
        ManagerSession other = await RegisterAsync();
        Guid foreign = await CreateGroupAsync(other, "Obras");

        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, ExpenseBody("single", Guid.NewGuid(), "Pintura", Month(0), amount: 10m));
        HttpResponseMessage foreignGroup = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, ExpenseBody("single", foreign, "Pintura", Month(0), amount: 10m));

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.CodeAsync()).Should().Be("expense_group.not_found");
        foreignGroup.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Despesas: consulta e edição ----

    [Fact]
    public async Task Get_lists_active_expenses_by_default_and_ended_ones_with_the_status_filter()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        JsonObject ended = ExpenseBody("fixed", group, "Jardinagem antiga", Month(-3), amount: 300m);
        ended["endMonth"] = Month(-1);
        await CreateExpenseAsync(manager, ended);
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(0), amount: 4200m));

        string[] active = await DescriptionsAsync(manager, Expenses);
        string[] explicitActive = await DescriptionsAsync(manager, $"{Expenses}?status=active");
        string[] endedOnly = await DescriptionsAsync(manager, $"{Expenses}?status=ended");
        string[] all = await DescriptionsAsync(manager, $"{Expenses}?status=all");

        active.Should().Equal("Portaria");
        explicitActive.Should().Equal("Portaria");
        endedOnly.Should().Equal("Jardinagem antiga");
        all.Should().BeEquivalentTo("Portaria", "Jardinagem antiga");
    }

    [Fact]
    public async Task Get_with_an_unknown_status_answers_400()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Expenses}?status=talvez", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("request.invalid");
    }

    [Fact]
    public async Task Put_edits_the_description_the_supplier_and_the_group_and_rotates_the_version()
    {
        ManagerSession manager = await RegisterAsync();
        Guid obras = await CreateGroupAsync(manager, "Obras");
        Guid contratos = await CreateGroupAsync(manager, "Contratos");
        JsonElement expense = await CreateExpenseAsync(manager, ExpenseBody("fixed", obras, "Portaria", Month(0), amount: 4200m));
        string route = $"{Expenses}/{expense.GetProperty("id").GetGuid()}";
        string version = expense.GetProperty("version").GetString()!;

        HttpResponseMessage edited = await _client.SendJsonAsync(
            HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["description"] = "Portaria 24h", ["supplier"] = "Segurança Ltda", ["groupId"] = contratos, ["dueDay"] = 15 });
        HttpResponseMessage stale = await _client.SendJsonAsync(
            HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["description"] = "Outra", ["groupId"] = contratos, ["dueDay"] = 15 });
        HttpResponseMessage missing = await _client.SendJsonAsync(
            HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["description"] = "Outra", ["groupId"] = contratos, ["dueDay"] = 15 });

        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await edited.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("Portaria 24h");
        body.GetProperty("supplier").GetString().Should().Be("Segurança Ltda");
        body.GetProperty("groupId").GetGuid().Should().Be(contratos);
        body.GetProperty("dueDay").GetInt32().Should().Be(15);
        body.GetProperty("version").GetString().Should().NotBe(version);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.CodeAsync()).Should().Be("finance.version.required");
    }

    [Fact]
    public async Task Put_of_an_unknown_expense_is_404()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Obras");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Expenses}/{Guid.NewGuid()}", manager.AccessToken, new JsonObject { ["version"] = "1", ["description"] = "X", ["groupId"] = group });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("expense.not_found");
    }

    [Fact]
    public async Task Changing_the_amount_of_a_fixed_expense_keeps_the_history()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        JsonElement expense = await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(-2), amount: 4000m));
        string route = $"{Expenses}/{expense.GetProperty("id").GetGuid()}/amount";
        string version = expense.GetProperty("version").GetString()!;

        HttpResponseMessage changed = await _client.SendJsonAsync(
            HttpMethod.Post, route, manager.AccessToken, new JsonObject { ["version"] = version, ["fromMonth"] = Month(0), ["amount"] = 4400m });
        HttpResponseMessage stale = await _client.SendJsonAsync(
            HttpMethod.Post, route, manager.AccessToken, new JsonObject { ["version"] = version, ["fromMonth"] = Month(1), ["amount"] = 4500m });

        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentAmount").GetDecimal().Should().Be(4400m);
        JsonElement[] changes = [.. body.GetProperty("amountChanges").EnumerateArray()];
        changes.Select(change => (change.GetProperty("fromMonth").GetString(), change.GetProperty("amount").GetDecimal())).Should().Equal((Month(-2), 4000m), (Month(0), 4400m));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Changing_the_amount_has_to_respect_the_kind_the_start_and_the_value()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        JsonElement single = await CreateExpenseAsync(manager, ExpenseBody("single", group, "Pintura", Month(0), amount: 100m));
        JsonElement fixedExpense = await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(0), amount: 4000m));

        HttpResponseMessage notFixed = await ChangeAmountAsync(manager, single, Month(0), 200m);
        HttpResponseMessage beforeStart = await ChangeAmountAsync(manager, fixedExpense, Month(-1), 4100m);
        HttpResponseMessage badAmount = await ChangeAmountAsync(manager, fixedExpense, Month(1), 0m);
        HttpResponseMessage badMonth = await ChangeAmountAsync(manager, fixedExpense, "abril", 4100m);

        notFixed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await notFixed.CodeAsync()).Should().Be("expense.not_fixed");
        beforeStart.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await beforeStart.CodeAsync()).Should().Be("expense.change_before_start");
        badAmount.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await badAmount.CodeAsync()).Should().Be("expense.amount.invalid");
        badMonth.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await badMonth.CodeAsync()).Should().Be("month.invalid");
    }

    [Fact]
    public async Task Put_changes_the_amount_of_one_parcel()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Obras");
        JsonElement expense = await CreateExpenseAsync(manager, ExpenseBody("installment", group, "Elevador", Month(0), amount: 600m, installmentCount: 6, currentInstallment: 1));
        string id = expense.GetProperty("id").GetGuid().ToString();
        string version = expense.GetProperty("version").GetString()!;

        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Put, $"{Expenses}/{id}/installments/9", manager.AccessToken, new JsonObject { ["version"] = version, ["amount"] = 120m });
        HttpResponseMessage changed = await _client.SendJsonAsync(HttpMethod.Put, $"{Expenses}/{id}/installments/2", manager.AccessToken, new JsonObject { ["version"] = version, ["amount"] = 120m });

        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("installments").EnumerateArray().Single(parcel => parcel.GetProperty("number").GetInt32() == 2).GetProperty("amount").GetDecimal().Should().Be(120m);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.CodeAsync()).Should().Be("expense.installment.not_found");
    }

    [Fact]
    public async Task Changing_a_parcel_of_an_expense_that_is_not_an_installment_is_422()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        JsonElement expense = await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(0), amount: 4000m));

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Expenses}/{expense.GetProperty("id").GetGuid()}/installments/1", manager.AccessToken, new JsonObject { ["version"] = expense.GetProperty("version").GetString(), ["amount"] = 1m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.CodeAsync()).Should().Be("expense.not_installment");
    }

    [Fact]
    public async Task Ending_a_variable_expense_sets_the_last_month_and_other_kinds_cannot_be_ended()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Consumo");
        JsonElement variable = await CreateExpenseAsync(manager, ExpenseBody("variable", group, "Água", Month(-2)));
        JsonElement single = await CreateExpenseAsync(manager, ExpenseBody("single", group, "Pintura", Month(0), amount: 100m));

        HttpResponseMessage ended = await EndAsync(manager, variable, Month(-1));
        HttpResponseMessage beforeStart = await EndAsync(manager, variable, Month(-5));
        HttpResponseMessage notAllowed = await EndAsync(manager, single, Month(0));

        ended.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await ended.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("endMonth").GetString().Should().Be(Month(-1));
        body.GetProperty("isEnded").GetBoolean().Should().BeTrue();
        notAllowed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await notAllowed.CodeAsync()).Should().Be("expense.end_not_allowed");
        beforeStart.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    // ---- Lançamentos do mês ----

    [Fact]
    public async Task A_month_that_was_never_opened_comes_back_empty()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{Month(-1)}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetString().Should().Be(Month(-1));
        body.GetProperty("entries").GetArrayLength().Should().Be(0);
        body.GetProperty("total").GetDecimal().Should().Be(0m);
        body.GetProperty("needsAmountCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Opening_the_month_generates_the_entries_with_labels_and_totals_per_group_and_can_be_repeated()
    {
        ManagerSession manager = await RegisterAsync();
        Guid contratos = await CreateGroupAsync(manager, "Contratos");
        Guid obras = await CreateGroupAsync(manager, "Obras");
        await CreateExpenseAsync(manager, ExpenseBody("fixed", contratos, "Portaria", Month(-2), amount: 4000m));
        await CreateExpenseAsync(manager, ExpenseBody("variable", contratos, "Água", Month(-2)));
        await CreateExpenseAsync(manager, ExpenseBody("installment", obras, "Elevador", Month(-1), amount: 600m, installmentCount: 6, currentInstallment: 3));

        HttpResponseMessage first = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/open", manager.AccessToken, new JsonObject());
        HttpResponseMessage second = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/open", manager.AccessToken, new JsonObject());

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await first.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement[] entries = [.. body.GetProperty("entries").EnumerateArray()];
        entries.Select(entry => entry.GetProperty("description").GetString()).Should().BeEquivalentTo("Portaria", "Água", "Elevador");
        entries.Single(entry => entry.GetProperty("description").GetString() == "Elevador").GetProperty("label").GetString().Should().Be("4/6");
        entries.Single(entry => entry.GetProperty("description").GetString() == "Água").GetProperty("needsAmount").GetBoolean().Should().BeTrue();
        entries.Single(entry => entry.GetProperty("description").GetString() == "Água").GetProperty("amount").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("total").GetDecimal().Should().Be(4100m);
        body.GetProperty("needsAmountCount").GetInt32().Should().Be(1);
        JsonElement[] totals = [.. body.GetProperty("groups").EnumerateArray()];
        totals.Single(group => group.GetProperty("groupId").GetGuid() == contratos).GetProperty("total").GetDecimal().Should().Be(4000m);
        totals.Single(group => group.GetProperty("groupId").GetGuid() == contratos).GetProperty("needsAmountCount").GetInt32().Should().Be(1);
        totals.Single(group => group.GetProperty("groupId").GetGuid() == obras).GetProperty("total").GetDecimal().Should().Be(100m);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("entries").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Get_returns_what_the_open_generated()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(-1), amount: 4000m));
        await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/open", manager.AccessToken, new JsonObject());

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{Month(0)}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("description").GetString()).Should().Equal("Portaria");
    }

    [Theory]
    [InlineData("abril")]
    [InlineData("2026-13")]
    [InlineData("2026-4")]
    public async Task A_malformed_month_answers_400(string month)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage read = await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{month}", manager.AccessToken);
        HttpResponseMessage open = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{month}/open", manager.AccessToken, new JsonObject());

        read.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await read.CodeAsync()).Should().Be("month.invalid");
        open.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Opening_a_month_up_to_five_years_ahead_works_and_further_answers_422()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage tooFar = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(61)}/open", manager.AccessToken, new JsonObject());
        HttpResponseMessage limit = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(60)}/open", manager.AccessToken, new JsonObject());
        HttpResponseMessage next = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(2)}/open", manager.AccessToken, new JsonObject());

        tooFar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await tooFar.CodeAsync()).Should().Be("month.too_far_ahead");
        limit.StatusCode.Should().Be(HttpStatusCode.OK);
        next.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_entries_come_with_the_due_date_of_their_month_and_start_pending()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Consumo");
        await CreateExpenseAsync(manager, ExpenseBody("single", group, "Troca da lâmpada", Month(1), amount: 250m, dueDay: 5));
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(1), amount: 4200m, dueDay: 31));

        JsonElement month = await OpenAsync(manager, Month(1));

        JsonElement[] entries = [.. month.GetProperty("entries").EnumerateArray()];
        JsonElement single = entries.Single(entry => entry.GetProperty("description").GetString() == "Troca da lâmpada");
        single.GetProperty("dueDate").GetString().Should().Be($"{Month(1)}-05");
        single.GetProperty("status").GetString().Should().Be("pending");
        string lastDay = entries.Single(entry => entry.GetProperty("description").GetString() == "Portaria").GetProperty("dueDate").GetString()!;
        DateOnly parsed = DateOnly.ParseExact(lastDay, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        parsed.Day.Should().Be(Math.Min(31, DateTime.DaysInMonth(parsed.Year, parsed.Month)));
        month.GetProperty("overdueCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task An_unpaid_entry_of_a_past_month_comes_back_as_overdue()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(-2), amount: 4200m, dueDay: 1));

        JsonElement month = await OpenAsync(manager, Month(-1));

        month.GetProperty("entries").EnumerateArray().Single().GetProperty("status").GetString().Should().Be("overdue");
        month.GetProperty("overdueCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Put_status_marks_an_entry_as_paid_and_back_and_the_overdue_comes_from_the_date()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(-2), amount: 4200m, dueDay: 1));
        JsonElement entry = (await OpenAsync(manager, Month(-1))).GetProperty("entries").EnumerateArray().Single();
        string route = $"{Entries}/{entry.GetProperty("id").GetGuid()}/status";

        HttpResponseMessage paid = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = entry.GetProperty("version").GetString(), ["status"] = "paid" });
        JsonElement paidBody = await paid.Content.ReadFromJsonAsync<JsonElement>();
        HttpResponseMessage undone = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = paidBody.GetProperty("version").GetString(), ["status"] = "pending" });

        paid.StatusCode.Should().Be(HttpStatusCode.OK);
        paidBody.GetProperty("status").GetString().Should().Be("paid");
        paidBody.GetProperty("version").GetString().Should().NotBe(entry.GetProperty("version").GetString());
        undone.StatusCode.Should().Be(HttpStatusCode.OK);
        (await undone.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("overdue");
        (await ReadMonthAsync(manager, Month(-1))).GetProperty("entries").EnumerateArray().Single().GetProperty("status").GetString().Should().Be("overdue");
    }

    [Fact]
    public async Task Put_status_canceled_leaves_the_entry_out_of_the_totals()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Contratos");
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(1), amount: 4200m));
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Jardinagem", Month(1), amount: 300m));
        JsonElement[] entries = [.. (await OpenAsync(manager, Month(1))).GetProperty("entries").EnumerateArray()];
        JsonElement garden = entries.Single(entry => entry.GetProperty("description").GetString() == "Jardinagem");

        HttpResponseMessage canceled = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Entries}/{garden.GetProperty("id").GetGuid()}/status", manager.AccessToken, new JsonObject { ["version"] = garden.GetProperty("version").GetString(), ["status"] = "canceled" });

        canceled.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement month = await ReadMonthAsync(manager, Month(1));
        month.GetProperty("total").GetDecimal().Should().Be(4200m);
        month.GetProperty("groups").EnumerateArray().Single().GetProperty("total").GetDecimal().Should().Be(4200m);
    }

    [Fact]
    public async Task Put_status_refuses_paying_without_an_amount_a_stale_version_a_bad_status_and_an_unknown_entry()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Consumo");
        await CreateExpenseAsync(manager, ExpenseBody("variable", group, "Água", Month(0)));
        JsonElement entry = (await OpenAsync(manager, Month(0))).GetProperty("entries").EnumerateArray().Single();
        string route = $"{Entries}/{entry.GetProperty("id").GetGuid()}/status";
        string version = entry.GetProperty("version").GetString()!;

        HttpResponseMessage noAmount = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["status"] = "paid" });
        HttpResponseMessage overdue = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["status"] = "overdue" });
        HttpResponseMessage unknownStatus = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["status"] = "talvez" });
        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["status"] = "canceled" });
        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = "0", ["status"] = "canceled" });
        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Put, $"{Entries}/{Guid.NewGuid()}/status", manager.AccessToken, new JsonObject { ["version"] = version, ["status"] = "canceled" });

        noAmount.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await noAmount.CodeAsync()).Should().Be("expense_entry.amount_required");
        overdue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await overdue.CodeAsync()).Should().Be("expense_entry.status.invalid");
        unknownStatus.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownStatus.CodeAsync()).Should().Be("expense_entry.status.invalid");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.CodeAsync()).Should().Be("finance.version.required");
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_fills_the_amount_of_a_pending_entry_and_the_next_month_suggests_it()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Consumo");
        await CreateExpenseAsync(manager, ExpenseBody("variable", group, "Água", Month(-1)));
        JsonElement previous = await OpenAsync(manager, Month(-1));
        JsonElement entry = previous.GetProperty("entries").EnumerateArray().Single();
        string version = entry.GetProperty("version").GetString()!;

        HttpResponseMessage filled = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Entries}/{entry.GetProperty("id").GetGuid()}/amount", manager.AccessToken, new JsonObject { ["version"] = version, ["amount"] = 812.34m });
        JsonElement current = await OpenAsync(manager, Month(0));

        filled.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await filled.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("amount").GetDecimal().Should().Be(812.34m);
        body.GetProperty("needsAmount").GetBoolean().Should().BeFalse();
        body.GetProperty("version").GetString().Should().NotBe(version);
        JsonElement next = current.GetProperty("entries").EnumerateArray().Single();
        next.GetProperty("needsAmount").GetBoolean().Should().BeTrue();
        next.GetProperty("suggestedAmount").GetDecimal().Should().Be(812.34m);
    }

    [Fact]
    public async Task Put_of_an_entry_refuses_a_stale_version_a_bad_amount_and_an_unknown_entry()
    {
        ManagerSession manager = await RegisterAsync();
        Guid group = await CreateGroupAsync(manager, "Consumo");
        await CreateExpenseAsync(manager, ExpenseBody("variable", group, "Água", Month(0)));
        JsonElement month = await OpenAsync(manager, Month(0));
        JsonElement entry = month.GetProperty("entries").EnumerateArray().Single();
        string route = $"{Entries}/{entry.GetProperty("id").GetGuid()}/amount";
        string version = entry.GetProperty("version").GetString()!;
        HttpResponseMessage filled = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["amount"] = 10m });
        filled.StatusCode.Should().Be(HttpStatusCode.OK);
        string current = (await filled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString()!;

        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["amount"] = 20m });
        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["amount"] = 20m });
        HttpResponseMessage invalid = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = current, ["amount"] = 0m });
        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Put, $"{Entries}/{Guid.NewGuid()}/amount", manager.AccessToken, new JsonObject { ["version"] = version, ["amount"] = 20m });

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.CodeAsync()).Should().Be("finance.version.required");
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.CodeAsync()).Should().Be("expense.amount.invalid");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.CodeAsync()).Should().Be("expense_entry.not_found");
    }

    // ---- Isolamento entre condomínios ----

    [Fact]
    public async Task One_condominium_never_sees_or_changes_the_data_of_another()
    {
        ManagerSession first = await RegisterAsync();
        ManagerSession second = await RegisterAsync();
        Guid group = await CreateGroupAsync(first, "Obras");
        JsonElement expense = await CreateExpenseAsync(first, ExpenseBody("variable", group, "Água", Month(0)));
        JsonElement month = await OpenAsync(first, Month(0));
        Guid entryId = month.GetProperty("entries").EnumerateArray().Single().GetProperty("id").GetGuid();

        string[] groups = await NamesAsync(second, Groups);
        string[] expenses = await DescriptionsAsync(second, Expenses);
        JsonElement otherMonth = await ReadMonthAsync(second, Month(0));
        HttpResponseMessage rename = await _client.SendJsonAsync(HttpMethod.Put, $"{Groups}/{group}", second.AccessToken, new JsonObject { ["version"] = "1", ["name"] = "Invadido" });
        HttpResponseMessage edit = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Expenses}/{expense.GetProperty("id").GetGuid()}", second.AccessToken, new JsonObject { ["version"] = "1", ["description"] = "Invadida", ["groupId"] = group, ["dueDay"] = 15 });
        HttpResponseMessage fill = await _client.SendJsonAsync(HttpMethod.Put, $"{Entries}/{entryId}/amount", second.AccessToken, new JsonObject { ["version"] = "1", ["amount"] = 1m });

        groups.Should().BeEmpty();
        expenses.Should().BeEmpty();
        otherMonth.GetProperty("entries").GetArrayLength().Should().Be(0);
        rename.StatusCode.Should().Be(HttpStatusCode.NotFound);
        edit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        fill.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Apoio ----

    private static string Month(int offset)
    {
        DateTimeOffset brasilia = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3));
        DateTime shifted = new DateTime(brasilia.Year, brasilia.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(offset);

        return shifted.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    private static JsonObject ExpenseBody(
        string kind,
        Guid groupId,
        string description,
        string startMonth,
        decimal? amount = null,
        string? supplier = null,
        int? installmentCount = null,
        int? currentInstallment = null,
        int? dueDay = 10) => new()
    {
        ["kind"] = kind,
        ["groupId"] = groupId,
        ["description"] = description,
        ["supplier"] = supplier,
        ["startMonth"] = startMonth,
        ["amount"] = amount,
        ["installmentCount"] = installmentCount,
        ["currentInstallment"] = currentInstallment,
        ["dueDay"] = dueDay,
    };

    // ---- Fechar e reabrir o mês ----

    [Fact]
    public async Task Closing_the_month_splits_the_total_by_ideal_fraction_and_shows_it_when_the_month_is_read()
    {
        ManagerSession manager = await RegisterAsync();
        Guid first = await CreateUnitAsync(manager, "101", 0.6m);
        Guid second = await CreateUnitAsync(manager, "102", 0.4m);
        await OpenMonthWithAFixedExpenseAsync(manager, 1000m);

        HttpResponseMessage response = await CloseAsync(manager, Month(0), "2099-12-10");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement closing = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("closing");
        closing.GetProperty("total").GetDecimal().Should().Be(1000m);
        closing.GetProperty("dueDate").GetString().Should().Be("2099-12-10");
        closing.GetProperty("basis").GetString().Should().Be("idealFraction");
        JsonElement[] charges = [.. closing.GetProperty("charges").EnumerateArray()];
        charges.Select(charge => charge.GetProperty("unitId").GetGuid()).Should().Equal(first, second);
        charges.Select(charge => charge.GetProperty("amount").GetDecimal()).Should().Equal(600m, 400m);
        charges[0].GetProperty("number").GetString().Should().Be("101");
        (await ReadMonthAsync(manager, Month(0))).GetProperty("closing").GetProperty("charges").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task A_closed_month_refuses_changes_and_closing_it_again_is_409()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateUnitAsync(manager, "101", 1m);
        JsonElement opened = await OpenMonthWithAFixedExpenseAsync(manager, 1000m);
        JsonElement entry = opened.GetProperty("entries")[0];
        await CloseAsync(manager, Month(0), "2099-12-10");

        HttpResponseMessage amount = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"{Entries}/{entry.GetProperty("id").GetGuid()}/amount",
            manager.AccessToken,
            new JsonObject { ["version"] = entry.GetProperty("version").GetString(), ["amount"] = 1200m });
        HttpResponseMessage again = await CloseAsync(manager, Month(0), "2099-12-10");

        amount.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await amount.CodeAsync()).Should().Be("closing.month_is_closed");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.CodeAsync()).Should().Be("closing.already_closed");
    }

    [Fact]
    public async Task Reopening_deletes_the_charges_lets_the_month_change_and_a_new_closing_uses_the_new_total()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateUnitAsync(manager, "101", 0.5m);
        await CreateUnitAsync(manager, "102", 0.5m);
        JsonElement opened = await OpenMonthWithAFixedExpenseAsync(manager, 1000m);
        JsonElement entry = opened.GetProperty("entries")[0];
        await CloseAsync(manager, Month(0), "2099-12-10");

        HttpResponseMessage reopened = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/reopen", manager.AccessToken, new JsonObject());
        HttpResponseMessage edited = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"{Entries}/{entry.GetProperty("id").GetGuid()}/amount",
            manager.AccessToken,
            new JsonObject { ["version"] = entry.GetProperty("version").GetString(), ["amount"] = 1200m });
        HttpResponseMessage closedAgain = await CloseAsync(manager, Month(0), "2099-12-15");

        reopened.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reopened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("closing").ValueKind.Should().Be(JsonValueKind.Null);
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement closing = (await closedAgain.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("closing");
        closing.GetProperty("total").GetDecimal().Should().Be(1200m);
        closing.GetProperty("dueDate").GetString().Should().Be("2099-12-15");
        closing.GetProperty("charges").EnumerateArray().Select(charge => charge.GetProperty("amount").GetDecimal()).Should().Equal(600m, 600m);
    }

    [Fact]
    public async Task Reopening_a_month_that_is_not_closed_is_409()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/reopen", manager.AccessToken, new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("closing.not_closed");
    }

    [Fact]
    public async Task Closing_without_units_ready_for_the_apportionment_is_422_and_says_why()
    {
        ManagerSession manager = await RegisterAsync();
        await OpenMonthWithAFixedExpenseAsync(manager, 1000m);

        HttpResponseMessage response = await CloseAsync(manager, Month(0), "2099-12-10");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.CodeAsync()).Should().Be("condominium.apportionment_not_ready");
    }

    [Fact]
    public async Task Closing_a_month_that_was_never_opened_or_still_waits_for_an_amount_is_422()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateUnitAsync(manager, "101", 1m);
        Guid group = await CreateGroupAsync(manager, "Contratos");

        HttpResponseMessage neverOpened = await CloseAsync(manager, Month(0), "2099-12-10");
        await CreateExpenseAsync(manager, ExpenseBody("variable", group, "Água", Month(0)));
        await OpenAsync(manager, Month(0));
        HttpResponseMessage waiting = await CloseAsync(manager, Month(0), "2099-12-10");

        neverOpened.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await neverOpened.CodeAsync()).Should().Be("closing.month_not_open");
        waiting.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await waiting.CodeAsync()).Should().Be("closing.amounts_pending");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10/12/2099")]
    [InlineData("2000-01-01")]
    public async Task Closing_with_a_missing_or_invalid_due_date_is_400(string? dueDate)
    {
        ManagerSession manager = await RegisterAsync();
        await CreateUnitAsync(manager, "101", 1m);
        await OpenMonthWithAFixedExpenseAsync(manager, 1000m);

        HttpResponseMessage response = await CloseAsync(manager, Month(0), dueDate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("closing.due_date_invalid");
    }

    [Fact]
    public async Task Closing_a_malformed_month_is_400()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await CloseAsync(manager, "abril", "2099-12-10");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("month.invalid");
    }

    private async Task<Guid> CreateUnitAsync(ManagerSession manager, string number, decimal idealFraction)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/units",
            manager.AccessToken,
            new JsonObject { ["number"] = number, ["type"] = "apartment", ["idealFraction"] = idealFraction });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> OpenMonthWithAFixedExpenseAsync(ManagerSession manager, decimal amount)
    {
        Guid group = await CreateGroupAsync(manager, "Contratos");
        await CreateExpenseAsync(manager, ExpenseBody("fixed", group, "Portaria", Month(0), amount: amount));

        return await OpenAsync(manager, Month(0));
    }

    private Task<HttpResponseMessage> CloseAsync(ManagerSession manager, string month, string? dueDate) =>
        _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{month}/close", manager.AccessToken, new JsonObject { ["dueDate"] = dueDate });

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private async Task<JsonElement> CreateGroupJsonAsync(ManagerSession manager, string name)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Groups, manager.AccessToken, new JsonObject { ["name"] = name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> CreateGroupAsync(ManagerSession manager, string name) => (await CreateGroupJsonAsync(manager, name)).GetProperty("id").GetGuid();

    private async Task<JsonElement> CreateExpenseAsync(ManagerSession manager, JsonObject body)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Expenses, manager.AccessToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> ChangeAmountAsync(ManagerSession manager, JsonElement expense, string fromMonth, decimal amount) =>
        _client.SendJsonAsync(
            HttpMethod.Post,
            $"{Expenses}/{expense.GetProperty("id").GetGuid()}/amount",
            manager.AccessToken,
            new JsonObject { ["version"] = expense.GetProperty("version").GetString(), ["fromMonth"] = fromMonth, ["amount"] = amount });

    private Task<HttpResponseMessage> EndAsync(ManagerSession manager, JsonElement expense, string lastMonth) =>
        _client.SendJsonAsync(
            HttpMethod.Post,
            $"{Expenses}/{expense.GetProperty("id").GetGuid()}/end",
            manager.AccessToken,
            new JsonObject { ["version"] = expense.GetProperty("version").GetString(), ["lastMonth"] = lastMonth });

    private async Task<JsonElement> OpenAsync(ManagerSession manager, string month)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{month}/open", manager.AccessToken, new JsonObject());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ReadMonthAsync(ManagerSession manager, string month)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{month}", manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string[]> DescriptionsAsync(ManagerSession manager, string route)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, route, manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(item => item.GetProperty("description").GetString()!)];
    }

    private async Task<string[]> NamesAsync(ManagerSession manager, string route)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, route, manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(item => item.GetProperty("name").GetString()!)];
    }
}
