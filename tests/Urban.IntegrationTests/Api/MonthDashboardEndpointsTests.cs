using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>O painel financeiro do mês de ponta a ponta. O mês fechado tem duas unidades, 101 (cobrança de 600) e 102 (400), e uma despesa de R$ 1.000,00.</summary>
[Collection(ApiCollection.Name)]
public sealed class MonthDashboardEndpointsTests(ApiFixture fixture) : IDisposable
{
    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    private static string Route(string month) => $"/api/finance/months/{month}/dashboard";

    // ---- Acesso ----

    [Fact]
    public async Task The_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route(Month(0)), null, null);

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

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route(Month(0)), token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    [Theory]
    [InlineData("x")]
    [InlineData("2026-13")]
    public async Task An_invalid_month_is_a_bad_request(string month)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await GetAsync(manager, month);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Painel ----

    [Fact]
    public async Task A_month_that_was_never_opened_is_empty_and_not_closed()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await GetAsync(manager, Month(0));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetString().Should().Be(Month(0));
        body.GetProperty("asOf").GetString().Should().Be(ClosedMonthKit.Today());
        body.GetProperty("closed").GetBoolean().Should().BeFalse();
        body.GetProperty("charges").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement expenses = body.GetProperty("expenses");
        expenses.GetProperty("total").GetDecimal().Should().Be(0m);
        expenses.GetProperty("needsAmountCount").GetInt32().Should().Be(0);
        expenses.GetProperty("overdueCount").GetInt32().Should().Be(0);
        expenses.GetProperty("groups").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_closed_month_shows_the_expenses_by_group_and_what_was_charged()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");

        JsonElement body = await ReadAsync(await GetAsync(manager, Month(0)));

        body.GetProperty("closed").GetBoolean().Should().BeTrue();
        JsonElement expenses = body.GetProperty("expenses");
        expenses.GetProperty("total").GetDecimal().Should().Be(1000m);
        JsonElement group = expenses.GetProperty("groups").EnumerateArray().Single();
        group.GetProperty("groupId").GetGuid().Should().NotBeEmpty();
        group.GetProperty("groupName").GetString().Should().Be("Contratos");
        group.GetProperty("total").GetDecimal().Should().Be(1000m);
        JsonElement charges = body.GetProperty("charges");
        charges.GetProperty("dueDate").GetString().Should().Be("2099-12-10");
        charges.GetProperty("charged").GetDecimal().Should().Be(1000m);
        charges.GetProperty("received").GetDecimal().Should().Be(0m);
        charges.GetProperty("balance").GetDecimal().Should().Be(1000m);
        charges.GetProperty("units").GetInt32().Should().Be(2);
        charges.GetProperty("open").GetInt32().Should().Be(2);
        charges.GetProperty("partial").GetInt32().Should().Be(0);
        charges.GetProperty("paid").GetInt32().Should().Be(0);
        charges.GetProperty("overdue").GetInt32().Should().Be(0);
        charges.GetProperty("overdueAmount").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Payments_move_the_units_to_paid_and_partial_and_a_reversal_takes_them_back()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        (Guid first, Guid second) = await ChargesAsync(manager, Month(0));

        Guid payment = await PayAsync(manager, first, 600m);
        await PayAsync(manager, second, 100m);
        JsonElement paid = (await ReadAsync(await GetAsync(manager, Month(0)))).GetProperty("charges");

        paid.GetProperty("received").GetDecimal().Should().Be(700m);
        paid.GetProperty("balance").GetDecimal().Should().Be(300m);
        paid.GetProperty("paid").GetInt32().Should().Be(1);
        paid.GetProperty("partial").GetInt32().Should().Be(1);
        paid.GetProperty("open").GetInt32().Should().Be(0);

        HttpResponseMessage reversal = await _client.SendJsonAsync(
            HttpMethod.Post, $"/api/finance/payments/{payment}/reverse", manager.AccessToken, new JsonObject { ["reason"] = "Pagamento duplicado" });
        reversal.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement reversed = (await ReadAsync(await GetAsync(manager, Month(0)))).GetProperty("charges");

        reversed.GetProperty("received").GetDecimal().Should().Be(100m);
        reversed.GetProperty("paid").GetInt32().Should().Be(0);
        reversed.GetProperty("partial").GetInt32().Should().Be(1);
        reversed.GetProperty("open").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task An_expired_month_counts_what_is_unpaid_as_overdue_with_its_balance()
    {
        ManagerSession manager = await RegisterAsync();
        string month = Month(-3);
        await ClosedMonthKit.CloseAsync(_client, manager, month, $"{month}-01");
        (Guid first, _) = await ChargesAsync(manager, month);
        await PayAsync(manager, first, 250m);

        JsonElement charges = (await ReadAsync(await GetAsync(manager, month))).GetProperty("charges");

        charges.GetProperty("overdue").GetInt32().Should().Be(2);
        charges.GetProperty("overdueAmount").GetDecimal().Should().Be(750m);
        charges.GetProperty("received").GetDecimal().Should().Be(250m);
    }

    [Fact]
    public async Task Another_condominium_never_sees_the_panel_of_the_first_one()
    {
        ManagerSession owner = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, owner, Month(0), "2099-12-10");
        ManagerSession stranger = await ManagerSetup.RegisterAsync(fixture, _client);

        JsonElement body = await ReadAsync(await GetAsync(stranger, Month(0)));

        body.GetProperty("closed").GetBoolean().Should().BeFalse();
        body.GetProperty("expenses").GetProperty("total").GetDecimal().Should().Be(0m);
        (await ReadAsync(await GetAsync(owner, Month(0)))).GetProperty("closed").GetBoolean().Should().BeTrue();
    }

    // ---- Apoio ----

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private Task<HttpResponseMessage> GetAsync(ManagerSession manager, string month) =>
        _client.SendJsonAsync(HttpMethod.Get, Route(month), manager.AccessToken);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<(Guid First, Guid Second)> ChargesAsync(ManagerSession manager, string month)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"/api/finance/months/{month}/payments", manager.AccessToken);
        JsonElement[] items = [.. (await ReadAsync(response)).GetProperty("items").EnumerateArray()];

        return (items[0].GetProperty("chargeId").GetGuid(), items[1].GetProperty("chargeId").GetGuid());
    }

    private async Task<Guid> PayAsync(ManagerSession manager, Guid chargeId, decimal amount)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post,
            $"/api/finance/charges/{chargeId}/payments",
            manager.AccessToken,
            new JsonObject { ["amount"] = amount, ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "pix" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
