using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>O registro manual de pagamentos das cobranças de um mês fechado, de ponta a ponta.</summary>
[Collection(ApiCollection.Name)]
public sealed class PaymentEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Finance = "/api/finance";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", $"{Finance}/months/2026-11/payments" },
        { "GET", $"{Finance}/charges/{Guid.Empty}/payments" },
        { "POST", $"{Finance}/charges/{Guid.Empty}/payments" },
        { "POST", $"{Finance}/payments/{Guid.Empty}/reverse" },
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

        foreach ((string method, string route) in new[]
        {
            ("GET", $"{Finance}/months/{Month(0)}/payments"),
            ("GET", $"{Finance}/charges/{Guid.NewGuid()}/payments"),
            ("POST", $"{Finance}/charges/{Guid.NewGuid()}/payments"),
            ("POST", $"{Finance}/payments/{Guid.NewGuid()}/reverse"),
        })
        {
            HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, token, method == "GET" ? null : new JsonObject());

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");
            (await response.CodeAsync()).Should().Be("identity.user.not_manager");
        }
    }

    // ---- Lista do mês ----

    [Fact]
    public async Task The_month_list_shows_each_charge_with_the_totals_of_the_month()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");

        HttpResponseMessage response = await ListMonthAsync(manager, Month(0));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetString().Should().Be(Month(0));
        body.GetProperty("dueDate").GetString().Should().Be("2099-12-10");
        body.GetProperty("charged").GetDecimal().Should().Be(1000m);
        body.GetProperty("received").GetDecimal().Should().Be(0m);
        body.GetProperty("total").GetInt32().Should().Be(2);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(50);
        JsonElement[] items = [.. body.GetProperty("items").EnumerateArray()];
        items.Select(item => item.GetProperty("number").GetString()).Should().Equal("101", "102");
        items.Select(item => item.GetProperty("amount").GetDecimal()).Should().Equal(600m, 400m);
        items.Should().OnlyContain(item => item.GetProperty("status").GetString() == "open" && item.GetProperty("balance").GetDecimal() == item.GetProperty("amount").GetDecimal());
        items[0].GetProperty("lastReceivedOn").ValueKind.Should().Be(JsonValueKind.Null);
        items[0].GetProperty("chargeId").GetGuid().Should().NotBe(Guid.Empty);
        items[0].GetProperty("unitId").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task The_month_list_follows_the_payments_partial_then_paid_and_filters_by_status()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, Guid second) = await ChargeIdsAsync(manager, Month(0));
        await RegisterPaymentAsync(manager, first, 600m);
        await RegisterPaymentAsync(manager, second, 100m);

        JsonElement all = await ReadAsync(await ListMonthAsync(manager, Month(0)));
        JsonElement paid = await ReadAsync(await ListMonthAsync(manager, Month(0), "status=paid"));
        JsonElement partial = await ReadAsync(await ListMonthAsync(manager, Month(0), "status=PARTIAL"));
        JsonElement open = await ReadAsync(await ListMonthAsync(manager, Month(0), "status=open"));

        all.GetProperty("received").GetDecimal().Should().Be(700m);
        all.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("status").GetString()).Should().Equal("paid", "partial");
        JsonElement partialItem = all.GetProperty("items")[1];
        partialItem.GetProperty("paid").GetDecimal().Should().Be(100m);
        partialItem.GetProperty("balance").GetDecimal().Should().Be(300m);
        partialItem.GetProperty("lastReceivedOn").GetString().Should().Be(ClosedMonthKit.Today());
        paid.GetProperty("items").GetArrayLength().Should().Be(1);
        paid.GetProperty("total").GetInt32().Should().Be(1);
        paid.GetProperty("received").GetDecimal().Should().Be(700m);
        partial.GetProperty("items")[0].GetProperty("chargeId").GetGuid().Should().Be(second);
        open.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_charge_past_its_due_date_without_full_payment_is_overdue()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(-3), $"{Month(-3)}-01");
        (Guid first, Guid second) = await ChargeIdsAsync(manager, Month(-3));
        await RegisterPaymentAsync(manager, first, 600m);

        JsonElement overdue = await ReadAsync(await ListMonthAsync(manager, Month(-3), "status=overdue"));

        overdue.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("chargeId").GetGuid()).Should().Equal(second);
    }

    [Fact]
    public async Task The_month_list_paginates()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");

        JsonElement page = await ReadAsync(await ListMonthAsync(manager, Month(0), "page=2&pageSize=1"));

        page.GetProperty("total").GetInt32().Should().Be(2);
        page.GetProperty("page").GetInt32().Should().Be(2);
        page.GetProperty("pageSize").GetInt32().Should().Be(1);
        page.GetProperty("items").EnumerateArray().Single().GetProperty("number").GetString().Should().Be("102");
    }

    [Fact]
    public async Task The_month_list_of_a_month_that_is_not_closed_is_409()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await ListMonthAsync(manager, Month(0));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("closing.not_closed");
    }

    [Theory]
    [InlineData("abril", "", "month.invalid")]
    [InlineData("", "status=quitado", "request.invalid")]
    [InlineData("", "page=0", "finance.payment.page_invalid")]
    [InlineData("", "pageSize=0", "finance.payment.page_invalid")]
    [InlineData("", "pageSize=201", "finance.payment.page_invalid")]
    public async Task The_month_list_refuses_a_bad_month_status_or_page(string month, string query, string code)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await ListMonthAsync(manager, month.Length == 0 ? Month(0) : month, query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    // ---- Registrar ----

    [Fact]
    public async Task Registering_answers_201_with_the_payment_and_where_to_list_them()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));

        HttpResponseMessage response = await RegisterPaymentRawAsync(
            manager, first, new JsonObject { ["amount"] = 250.50m, ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "transfer", ["note"] = "  Pagou pelo banco  " });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Location!.ToString().Should().EndWith($"{Finance}/charges/{first}/payments");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        body.GetProperty("chargeId").GetGuid().Should().Be(first);
        body.GetProperty("amount").GetDecimal().Should().Be(250.50m);
        body.GetProperty("receivedOn").GetString().Should().Be(ClosedMonthKit.Today());
        body.GetProperty("method").GetString().Should().Be("transfer");
        body.GetProperty("note").GetString().Should().Be("Pagou pelo banco");
        body.GetProperty("reversedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Several_payments_add_up_until_the_charge_is_paid_and_one_more_cent_is_422_with_the_balance()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));
        await RegisterPaymentAsync(manager, first, 200m);
        await RegisterPaymentAsync(manager, first, 399.99m);

        HttpResponseMessage tooMuch = await RegisterPaymentRawAsync(manager, first, PaymentBody(0.02m));
        HttpResponseMessage exact = await RegisterPaymentRawAsync(manager, first, PaymentBody(0.01m));

        tooMuch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonElement problem = await tooMuch.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("finance.payment.exceeds_balance");
        problem.GetProperty("data").GetProperty("balance").GetDecimal().Should().Be(0.01m);
        exact.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadAsync(await ListChargeAsync(manager, first))).GetProperty("status").GetString().Should().Be("paid");
    }

    [Theory]
    [InlineData(0, "pix", 0, "finance.payment.amount_invalid")]
    [InlineData(-5, "pix", 0, "finance.payment.amount_invalid")]
    [InlineData(10.005, "pix", 0, "finance.payment.amount_invalid")]
    [InlineData(100, "cheque", 0, "finance.payment.method_invalid")]
    [InlineData(100, "", 0, "finance.payment.method_invalid")]
    [InlineData(100, "pix", 1, "finance.payment.date_invalid")]
    public async Task Registering_with_an_invalid_field_is_400_with_the_code_of_the_field(double amount, string method, int daysFromToday, string code)
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));

        HttpResponseMessage response = await RegisterPaymentRawAsync(
            manager, first, new JsonObject { ["amount"] = (decimal)amount, ["receivedOn"] = ClosedMonthKit.Today(daysFromToday), ["method"] = method });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
        (await ReadAsync(await ListChargeAsync(manager, first))).GetProperty("payments").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("05/11/2026")]
    [InlineData("2000-01-01")]
    public async Task Registering_with_a_missing_malformed_or_too_old_date_is_400(string? receivedOn)
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));

        HttpResponseMessage response = await RegisterPaymentRawAsync(manager, first, new JsonObject { ["amount"] = 100m, ["receivedOn"] = receivedOn, ["method"] = "pix" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("finance.payment.date_invalid");
    }

    [Fact]
    public async Task Registering_without_an_amount_or_with_a_long_note_is_400()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));

        HttpResponseMessage noAmount = await RegisterPaymentRawAsync(manager, first, new JsonObject { ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "pix" });
        HttpResponseMessage longNote = await RegisterPaymentRawAsync(
            manager, first, new JsonObject { ["amount"] = 10m, ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "pix", ["note"] = new string('a', 201) });

        noAmount.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noAmount.CodeAsync()).Should().Be("finance.payment.amount_invalid");
        longNote.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await longNote.CodeAsync()).Should().Be("finance.payment.note_invalid");
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_400_request_invalid()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));
        using HttpRequestMessage request = new(HttpMethod.Post, $"{Finance}/charges/{first}/payments")
        {
            Content = new StringContent("{ quebrado", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", manager.AccessToken);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("request.invalid");
    }

    [Fact]
    public async Task Registering_on_a_charge_that_does_not_exist_is_404()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await RegisterPaymentRawAsync(manager, Guid.NewGuid(), PaymentBody(100m));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.charge.not_found");
    }

    [Fact]
    public async Task The_charge_and_the_payment_of_another_condominium_are_404()
    {
        ManagerSession owner = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, owner, Month(0), "2099-12-10");
        (Guid charge, _) = await ChargeIdsAsync(owner, Month(0));
        Guid paymentId = await RegisterPaymentAsync(owner, charge, 100m);
        ManagerSession stranger = await RegisterAsync();

        HttpResponseMessage register = await RegisterPaymentRawAsync(stranger, charge, PaymentBody(100m));
        HttpResponseMessage list = await ListChargeAsync(stranger, charge);
        HttpResponseMessage reverse = await ReverseAsync(stranger, paymentId, "Erro");
        HttpResponseMessage month = await ListMonthAsync(stranger, Month(0));

        register.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await register.CodeAsync()).Should().Be("finance.charge.not_found");
        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        reverse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await reverse.CodeAsync()).Should().Be("finance.payment.not_found");
        month.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadAsync(await ListChargeAsync(owner, charge))).GetProperty("payments").GetArrayLength().Should().Be(1);
    }

    // ---- Lista de uma cobrança ----

    [Fact]
    public async Task The_charge_list_shows_the_payments_newest_first_with_what_was_paid_and_the_balance()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));
        Guid older = await RegisterPaymentAsync(manager, first, 100m);
        Guid wrong = await RegisterPaymentAsync(manager, first, 50m);
        Guid newer = await RegisterPaymentAsync(manager, first, 25m);
        (await ReverseAsync(manager, wrong, "Lançado na unidade errada")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage response = await ListChargeAsync(manager, first);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("chargeId").GetGuid().Should().Be(first);
        body.GetProperty("month").GetString().Should().Be(Month(0));
        body.GetProperty("dueDate").GetString().Should().Be("2099-12-10");
        body.GetProperty("amount").GetDecimal().Should().Be(600m);
        body.GetProperty("paid").GetDecimal().Should().Be(125m);
        body.GetProperty("balance").GetDecimal().Should().Be(475m);
        body.GetProperty("status").GetString().Should().Be("partial");
        JsonElement[] payments = [.. body.GetProperty("payments").EnumerateArray()];
        payments.Select(payment => payment.GetProperty("id").GetGuid()).Should().Equal(newer, wrong, older);
        payments[1].GetProperty("reversedAt").ValueKind.Should().Be(JsonValueKind.String);
        payments[1].GetProperty("reversalReason").GetString().Should().Be("Lançado na unidade errada");
        payments[0].GetProperty("reversedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_charge_list_of_an_unknown_charge_is_404()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await ListChargeAsync(manager, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.charge.not_found");
    }

    // ---- Estornar ----

    [Fact]
    public async Task Reversing_answers_204_and_the_amount_is_free_again()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));
        Guid payment = await RegisterPaymentAsync(manager, first, 600m);

        HttpResponseMessage reversed = await ReverseAsync(manager, payment, "  Unidade errada  ");
        HttpResponseMessage again = await RegisterPaymentRawAsync(manager, first, PaymentBody(600m));

        reversed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        reversed.Headers.CacheControl!.NoStore.Should().BeTrue();
        again.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement charge = await ReadAsync(await ListChargeAsync(manager, first));
        charge.GetProperty("payments").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == payment).GetProperty("reversalReason").GetString().Should().Be("Unidade errada");
    }

    [Fact]
    public async Task Reversing_twice_is_409()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));
        Guid payment = await RegisterPaymentAsync(manager, first, 100m);
        await ReverseAsync(manager, payment, "Erro");

        HttpResponseMessage again = await ReverseAsync(manager, payment, "Outro motivo");

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.CodeAsync()).Should().Be("finance.payment.already_reversed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reversing_without_a_reason_is_400_and_the_payment_stays(string? reason)
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));
        Guid payment = await RegisterPaymentAsync(manager, first, 100m);

        HttpResponseMessage response = await ReverseAsync(manager, payment, reason);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("finance.payment.reason_invalid");
        (await ReadAsync(await ListChargeAsync(manager, first))).GetProperty("paid").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task Reversing_a_payment_that_does_not_exist_is_404()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await ReverseAsync(manager, Guid.NewGuid(), "Erro");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.payment.not_found");
    }

    // ---- Reabrir o mês ----

    [Fact]
    public async Task Reopening_a_month_with_payments_is_409_even_after_every_payment_was_reversed()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, _) = await ChargeIdsAsync(manager, Month(0));
        Guid payment = await RegisterPaymentAsync(manager, first, 100m);

        HttpResponseMessage withPayment = await ReopenAsync(manager, Month(0));
        await ReverseAsync(manager, payment, "Erro");
        HttpResponseMessage afterReversal = await ReopenAsync(manager, Month(0));

        withPayment.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await withPayment.CodeAsync()).Should().Be("closing.has_payments");
        afterReversal.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await afterReversal.CodeAsync()).Should().Be("closing.has_payments");
        (await ListMonthAsync(manager, Month(0))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reopening_a_month_without_payments_still_works()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");

        (await ReopenAsync(manager, Month(0))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Apoio ----

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private static JsonObject PaymentBody(decimal amount) => new() { ["amount"] = amount, ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "pix" };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> ListMonthAsync(ManagerSession manager, string month, string query = "") =>
        _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/months/{month}/payments{(query.Length == 0 ? "" : "?" + query)}", manager.AccessToken);

    private Task<HttpResponseMessage> ListChargeAsync(ManagerSession manager, Guid chargeId) =>
        _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/charges/{chargeId}/payments", manager.AccessToken);

    private Task<HttpResponseMessage> RegisterPaymentRawAsync(ManagerSession manager, Guid chargeId, JsonObject body) =>
        _client.SendJsonAsync(HttpMethod.Post, $"{Finance}/charges/{chargeId}/payments", manager.AccessToken, body);

    private async Task<Guid> RegisterPaymentAsync(ManagerSession manager, Guid chargeId, decimal amount)
    {
        HttpResponseMessage response = await RegisterPaymentRawAsync(manager, chargeId, PaymentBody(amount));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> ReverseAsync(ManagerSession manager, Guid paymentId, string? reason) =>
        _client.SendJsonAsync(HttpMethod.Post, $"{Finance}/payments/{paymentId}/reverse", manager.AccessToken, new JsonObject { ["reason"] = reason });

    private Task<HttpResponseMessage> ReopenAsync(ManagerSession manager, string month) =>
        _client.SendJsonAsync(HttpMethod.Post, $"{Finance}/months/{month}/reopen", manager.AccessToken, new JsonObject());

    private async Task<(Guid First, Guid Second)> ChargeIdsAsync(ManagerSession manager, string month)
    {
        JsonElement list = await ReadAsync(await ListMonthAsync(manager, month));
        JsonElement[] items = [.. list.GetProperty("items").EnumerateArray()];

        return (items[0].GetProperty("chargeId").GetGuid(), items[1].GetProperty("chargeId").GetGuid());
    }
}
