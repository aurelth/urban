using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// O financeiro de quem mora, de ponta a ponta: a cobrança da unidade e as despesas do mês (sem fornecedor nem observação) e o PDF
/// da própria fatura. O mês fechado tem duas unidades, 101 (cobrança de 600) e 102 (400), e uma despesa de R$ 1.000,00.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyFinanceEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Finance = "/api/finance/me";
    private const string AccessRoute = Finance + "/access";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    private static string MonthRoute(string month) => $"{Finance}/months/{month}";

    private static string InvoiceRoute(Guid chargeId) => $"{Finance}/charges/{chargeId}/invoice";

    // ---- Acesso ----

    [Fact]
    public async Task The_routes_require_a_token()
    {
        HttpResponseMessage month = await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), null);
        HttpResponseMessage invoice = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(Guid.NewGuid()), null);

        month.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        invoice.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_super_admin_has_no_condominium_and_is_refused()
    {
        await MigrateAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: UserRole.SuperAdmin);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage month = await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token);
        HttpResponseMessage invoice = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(Guid.NewGuid()), token);

        month.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await month.CodeAsync()).Should().Be("identity.user.not_condominium_member");
        invoice.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("2026-13")]
    public async Task An_invalid_month_is_a_bad_request(string month)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(month), manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Como a conta mora (o que o menu do síndico pergunta) ----

    [Fact]
    public async Task The_access_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_access_route_refuses_the_super_admin()
    {
        await MigrateAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: UserRole.SuperAdmin);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_condominium_member");
    }

    [Fact]
    public async Task A_resident_who_lives_in_a_unit_has_unit_access_and_the_answer_is_never_cached()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unitId = await CreateUnitAsync(manager, "101");
        string owner = await ResidentTokenAsync(manager, unitId);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, owner);

        (await ReadAsync(response)).GetProperty("access").GetString().Should().Be("unit");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task A_dependent_only_has_dependent_access()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unitId = await CreateUnitAsync(manager, "101");
        string dependent = await ResidentTokenAsync(manager, unitId, "dependent");

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, dependent));

        body.GetProperty("access").GetString().Should().Be("dependent");
    }

    [Fact]
    public async Task A_manager_who_does_not_live_in_the_building_has_no_access()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateUnitAsync(manager, "101");

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, manager.AccessToken));

        body.GetProperty("access").GetString().Should().Be("none");
    }

    [Fact]
    public async Task A_manager_who_lives_in_a_unit_has_unit_access()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unitId = await CreateUnitAsync(manager, "101");
        await MakeManagerLiveInAsync(manager, unitId, endDate: null);

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, manager.AccessToken));

        body.GetProperty("access").GetString().Should().Be("unit");
    }

    [Fact]
    public async Task A_manager_who_already_moved_out_has_no_access()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unitId = await CreateUnitAsync(manager, "101");
        await MakeManagerLiveInAsync(manager, unitId, endDate: new DateOnly(2024, 6, 30));

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, manager.AccessToken));

        body.GetProperty("access").GetString().Should().Be("none");
    }

    // ---- A cobrança e as despesas do mês ----

    [Fact]
    public async Task A_resident_sees_the_charge_of_the_unit_and_the_expenses_of_the_month()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetString().Should().Be(Month(0));
        body.GetProperty("asOf").GetString().Should().Be(ClosedMonthKit.Today());
        body.GetProperty("access").GetString().Should().Be("unit");
        body.GetProperty("closed").GetBoolean().Should().BeTrue();
        JsonElement unit = body.GetProperty("units").EnumerateArray().Single();
        unit.GetProperty("unitId").GetGuid().Should().Be(units["101"]);
        unit.GetProperty("number").GetString().Should().Be("101");
        unit.GetProperty("block").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement charge = unit.GetProperty("charge");
        charge.GetProperty("chargeId").GetGuid().Should().Be(await ChargeOfAsync(manager, Month(0), units["101"]));
        charge.GetProperty("dueDate").GetString().Should().Be("2099-12-10");
        charge.GetProperty("amount").GetDecimal().Should().Be(600m);
        charge.GetProperty("paid").GetDecimal().Should().Be(0m);
        charge.GetProperty("balance").GetDecimal().Should().Be(600m);
        charge.GetProperty("status").GetString().Should().Be("open");
        charge.GetProperty("lastReceivedOn").ValueKind.Should().Be(JsonValueKind.Null);
        charge.GetProperty("payments").GetArrayLength().Should().Be(0);
        JsonElement expenses = body.GetProperty("expenses");
        expenses.GetProperty("total").GetDecimal().Should().Be(1000m);
        JsonElement group = expenses.GetProperty("groups").EnumerateArray().Single();
        group.GetProperty("groupId").GetGuid().Should().NotBeEmpty();
        group.GetProperty("groupName").GetString().Should().Be("Contratos");
        group.GetProperty("total").GetDecimal().Should().Be(1000m);
    }

    [Fact]
    public async Task The_resident_never_sees_the_supplier_the_note_nor_what_the_building_collected()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10", supplier: "Segurança Ltda", note: "OS 4521");
        string token = await ResidentTokenAsync(manager, units["101"]);

        string json = await (await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token)).Content.ReadAsStringAsync(Ct);

        json.Should().NotContain("Segurança").And.NotContain("OS 4521").And.NotContain("Portaria", "a descrição do lançamento também fica com o síndico");
        JsonElement body = JsonDocument.Parse(json).RootElement;
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("month", "asOf", "access", "closed", "units", "expenses");
        body.GetProperty("expenses").EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("total", "groups");
        body.GetProperty("units")[0].GetProperty("charge").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("chargeId", "dueDate", "amount", "paid", "balance", "status", "lastReceivedOn", "payments");
    }

    [Fact]
    public async Task The_payments_of_the_charge_carry_the_receipt_and_nothing_the_manager_keeps_to_himself()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid charge = await ChargeOfAsync(manager, Month(0), units["101"]);
        Guid first = await PayAsync(manager, charge, 200m, note: "Anotação do síndico");
        Guid second = await PayAsync(manager, charge, 100m);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token);

        string json = await response.Content.ReadAsStringAsync(Ct);
        json.Should().NotContain("Anotação do síndico");
        JsonElement payments = JsonDocument.Parse(json).RootElement.GetProperty("units")[0].GetProperty("charge").GetProperty("payments");
        payments.GetArrayLength().Should().Be(2);
        payments[0].EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("paymentId", "amount", "receivedOn", "method", "receiptNumber", "receiptCode", "reversed");
        payments[0].GetProperty("paymentId").GetGuid().Should().Be(first);
        payments[0].GetProperty("amount").GetDecimal().Should().Be(200m);
        payments[0].GetProperty("receivedOn").GetString().Should().Be(ClosedMonthKit.Today());
        payments[0].GetProperty("method").GetString().Should().Be("pix");
        payments[0].GetProperty("receiptNumber").GetInt32().Should().Be(1);
        payments[0].GetProperty("receiptCode").GetString().Should().Be("REC-000001");
        payments[0].GetProperty("reversed").GetBoolean().Should().BeFalse();
        payments[1].GetProperty("paymentId").GetGuid().Should().Be(second);
        payments[1].GetProperty("receiptCode").GetString().Should().Be("REC-000002");
    }

    [Fact]
    public async Task A_reversed_payment_stays_in_the_list_marked_as_reversed_and_its_receipt_can_still_be_downloaded()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid charge = await ChargeOfAsync(manager, Month(0), units["101"]);
        Guid payment = await PayAsync(manager, charge, 200m);
        HttpResponseMessage reversal = await _client.SendJsonAsync(
            HttpMethod.Post, $"/api/finance/payments/{payment}/reverse", manager.AccessToken, new JsonObject { ["reason"] = "Pagamento duplicado" });
        reversal.StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token));
        HttpResponseMessage receipt = await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/payments/{payment}/receipt", token);

        JsonElement chargeBody = body.GetProperty("units")[0].GetProperty("charge");
        chargeBody.GetProperty("paid").GetDecimal().Should().Be(0m);
        chargeBody.GetProperty("payments").EnumerateArray().Single().GetProperty("reversed").GetBoolean().Should().BeTrue();
        chargeBody.GetProperty("payments")[0].GetProperty("paymentId").GetGuid().Should().Be(payment);
        body.GetRawText().Should().NotContain("Pagamento duplicado", "o motivo do estorno fica com o síndico");
        receipt.StatusCode.Should().Be(HttpStatusCode.OK, await receipt.Content.ReadAsStringAsync(Ct));
        Encoding.ASCII.GetString(await receipt.Content.ReadAsByteArrayAsync(Ct), 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Every_payment_of_the_list_has_a_receipt_the_resident_can_download()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid charge = await ChargeOfAsync(manager, Month(0), units["101"]);
        await PayAsync(manager, charge, 200m);
        await PayAsync(manager, charge, 100m);

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token));

        JsonElement payments = body.GetProperty("units")[0].GetProperty("charge").GetProperty("payments");
        payments.GetArrayLength().Should().Be(2);

        foreach (JsonElement item in payments.EnumerateArray())
        {
            HttpResponseMessage receipt = await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/payments/{item.GetProperty("paymentId").GetGuid()}/receipt", token);

            receipt.StatusCode.Should().Be(HttpStatusCode.OK, await receipt.Content.ReadAsStringAsync(Ct));
            receipt.Content.Headers.ContentDisposition!.FileNameStar.Should().Be($"recibo-{item.GetProperty("receiptNumber").GetInt32():D6}.pdf");
        }
    }

    [Fact]
    public async Task A_resident_only_sees_his_own_unit_and_the_payments_of_the_neighbour_do_not_show()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["102"]);
        Guid neighbourPayment = await PayAsync(manager, await ChargeOfAsync(manager, Month(0), units["101"]), 600m);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token);
        string json = await response.Content.ReadAsStringAsync(Ct);
        json.Should().NotContain(neighbourPayment.ToString());
        JsonElement body = JsonDocument.Parse(json).RootElement;

        JsonElement unit = body.GetProperty("units").EnumerateArray().Single();
        unit.GetProperty("number").GetString().Should().Be("102");
        unit.GetProperty("charge").GetProperty("amount").GetDecimal().Should().Be(400m);
        unit.GetProperty("charge").GetProperty("status").GetString().Should().Be("open");
        unit.GetProperty("charge").GetProperty("payments").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Payments_change_the_situation_and_a_reversal_takes_it_back()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid charge = await ChargeOfAsync(manager, Month(0), units["101"]);

        Guid partialPayment = await PayAsync(manager, charge, 200m);
        JsonElement partial = (await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token))).GetProperty("units")[0].GetProperty("charge");
        await PayAsync(manager, charge, 400m);
        JsonElement paid = (await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token))).GetProperty("units")[0].GetProperty("charge");
        HttpResponseMessage reversal = await _client.SendJsonAsync(
            HttpMethod.Post, $"/api/finance/payments/{partialPayment}/reverse", manager.AccessToken, new JsonObject { ["reason"] = "Pagamento duplicado" });
        reversal.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement reversed = (await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token))).GetProperty("units")[0].GetProperty("charge");

        (partial.GetProperty("status").GetString(), partial.GetProperty("paid").GetDecimal(), partial.GetProperty("balance").GetDecimal()).Should().Be(("partial", 200m, 400m));
        partial.GetProperty("lastReceivedOn").GetString().Should().Be(ClosedMonthKit.Today());
        (paid.GetProperty("status").GetString(), paid.GetProperty("paid").GetDecimal(), paid.GetProperty("balance").GetDecimal()).Should().Be(("paid", 600m, 0m));
        (reversed.GetProperty("status").GetString(), reversed.GetProperty("paid").GetDecimal(), reversed.GetProperty("balance").GetDecimal()).Should().Be(("partial", 400m, 200m));
    }

    [Fact]
    public async Task An_expired_charge_is_overdue()
    {
        string month = Month(-3);
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, month, $"{month}-01");
        string token = await ResidentTokenAsync(manager, (await UnitsAsync(manager))["101"]);

        JsonElement charge = (await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(month), token))).GetProperty("units")[0].GetProperty("charge");

        charge.GetProperty("status").GetString().Should().Be("overdue");
        charge.GetProperty("balance").GetDecimal().Should().Be(600m);
    }

    [Fact]
    public async Task A_month_that_is_not_closed_has_no_charge_and_hides_the_expenses()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(1)), token));

        body.GetProperty("access").GetString().Should().Be("unit");
        body.GetProperty("closed").GetBoolean().Should().BeFalse();
        body.GetProperty("units")[0].GetProperty("charge").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("expenses").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_dependent_only_gets_the_notice_and_the_manager_without_a_unit_sees_nothing_financial()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string dependent = await ResidentTokenAsync(manager, units["101"], "dependent");

        JsonElement forTheDependent = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), dependent));
        JsonElement forTheManager = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), manager.AccessToken));

        foreach (JsonElement body in new[] { forTheDependent, forTheManager })
        {
            body.GetProperty("closed").GetBoolean().Should().BeFalse();
            body.GetProperty("units").GetArrayLength().Should().Be(0);
            body.GetProperty("expenses").ValueKind.Should().Be(JsonValueKind.Null);
        }

        forTheDependent.GetProperty("access").GetString().Should().Be("dependent");
        forTheManager.GetProperty("access").GetString().Should().Be("none");
    }

    [Fact]
    public async Task A_tenant_and_an_owner_of_the_same_unit_each_see_the_charge()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string owner = await ResidentTokenAsync(manager, units["101"], "owner");
        string tenant = await ResidentTokenAsync(manager, units["101"], "tenant");

        foreach (string token in new[] { owner, tenant })
        {
            JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, MonthRoute(Month(0)), token));

            body.GetProperty("units").EnumerateArray().Single().GetProperty("charge").GetProperty("amount").GetDecimal().Should().Be(600m);
        }
    }

    // ---- A fatura em PDF ----

    [Fact]
    public async Task The_resident_downloads_the_pdf_of_his_own_charge()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid charge = await ChargeOfAsync(manager, Month(0), units["101"]);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(charge), token);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().MatchRegex(@"^fatura-\d{4}-\d{2}-101\.pdf$");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync(Ct), 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task The_resident_cannot_download_the_invoice_of_another_unit_and_it_looks_like_the_charge_does_not_exist()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid neighbour = await ChargeOfAsync(manager, Month(0), units["102"]);

        HttpResponseMessage other = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(neighbour), token);
        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(Guid.NewGuid()), token);

        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.CodeAsync()).Should().Be("finance.charge.not_found");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.CodeAsync()).Should().Be("finance.charge.not_found");
    }

    [Fact]
    public async Task A_dependent_and_a_manager_without_a_unit_cannot_download_any_invoice_here()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        string dependent = await ResidentTokenAsync(manager, units["101"], "dependent");
        Guid charge = await ChargeOfAsync(manager, Month(0), units["101"]);

        HttpResponseMessage forTheDependent = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(charge), dependent);
        HttpResponseMessage forTheManager = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(charge), manager.AccessToken);

        forTheDependent.StatusCode.Should().Be(HttpStatusCode.NotFound);
        forTheManager.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_resident_of_another_condominium_never_gets_the_invoice()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync("2099-12-10");
        Guid charge = await ChargeOfAsync(manager, Month(0), units["101"]);
        ManagerSession stranger = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid strangerUnit = await CreateUnitAsync(stranger, "101");
        string token = await ResidentTokenAsync(stranger, strangerUnit);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, InvoiceRoute(charge), token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Apoio ----

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private async Task<(ManagerSession Manager, Dictionary<string, Guid> Units)> ClosedMonthAsync(string dueDate, string? supplier = null, string? note = null)
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), dueDate, supplier, note);

        return (manager, await UnitsAsync(manager));
    }

    private async Task<Dictionary<string, Guid>> UnitsAsync(ManagerSession manager)
    {
        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, "/api/units", manager.AccessToken));

        return body.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("number").GetString()!, item => item.GetProperty("id").GetGuid());
    }

    private Task<Guid> CreateUnitAsync(ManagerSession manager, string number) => ResidentSetup.CreateUnitAsync(_client, manager, number);

    private Task<string> ResidentTokenAsync(ManagerSession manager, Guid unitId, string kind = "owner") =>
        ResidentSetup.TokenAsync(fixture, _client, manager, unitId, kind);

    /// <summary>O vínculo do síndico é gravado direto no banco para o teste do financeiro não depender de outra rota; o caminho pela API tem os testes de `ResidentSelfEndpointsTests`.</summary>
    private async Task MakeManagerLiveInAsync(ManagerSession manager, Guid unitId, DateOnly? endDate)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        CondominiumDbContext db = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();
        Person person = await db.People.IgnoreQueryFilters().SingleAsync(p => p.UserId == manager.User.Id, Ct);
        DateOnly start = new(2024, 3, 1);
        Occupancy occupancy = Occupancy.Start(manager.CondominiumId, person.Id, unitId, OccupancyKind.Owner, start, new DateOnly(2026, 11, 7), DateTimeOffset.UtcNow).Value;

        if (endDate is { } end)
        {
            occupancy.MoveOut(end, new DateOnly(2026, 11, 7)).IsSuccess.Should().BeTrue();
        }

        db.Occupancies.Add(occupancy);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> ChargeOfAsync(ManagerSession manager, string month, Guid unitId)
    {
        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, $"/api/finance/months/{month}/payments", manager.AccessToken));

        return body.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("unitId").GetGuid() == unitId).GetProperty("chargeId").GetGuid();
    }

    private async Task<Guid> PayAsync(ManagerSession manager, Guid chargeId, decimal amount, string? note = null)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post,
            $"/api/finance/charges/{chargeId}/payments",
            manager.AccessToken,
            new JsonObject { ["amount"] = amount, ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "pix", ["note"] = note });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
