using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>A inadimplência de ponta a ponta. O mês fechado tem duas unidades, 101 (cobrança de 600) e 102 (400), e a lista é do condomínio do síndico.</summary>
[Collection(ApiCollection.Name)]
public sealed class DelinquencyEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Route = "/api/finance/delinquency";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    // ---- Acesso ----

    [Fact]
    public async Task The_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route, null, null);

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

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route, token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    // ---- Lista ----

    [Fact]
    public async Task A_condominium_without_closed_months_has_no_debtors()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await ListAsync(manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("asOf").GetString().Should().Be(ClosedMonthKit.Today());
        body.GetProperty("totalOverdue").GetDecimal().Should().Be(0m);
        body.GetProperty("units").GetInt32().Should().Be(0);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(50);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_month_that_has_not_expired_yet_does_not_count()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");

        JsonElement body = await ReadAsync(await ListAsync(manager));

        body.GetProperty("units").GetInt32().Should().Be(0);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task An_expired_month_lists_each_unit_with_what_it_owes_and_since_when()
    {
        ManagerSession manager = await RegisterAsync();
        string month = Month(-3);
        await ClosedMonthKit.CloseAsync(_client, manager, month, $"{month}-01");

        JsonElement body = await ReadAsync(await ListAsync(manager));

        body.GetProperty("totalOverdue").GetDecimal().Should().Be(1000m);
        body.GetProperty("units").GetInt32().Should().Be(2);
        JsonElement[] items = [.. body.GetProperty("items").EnumerateArray()];
        items.Select(item => item.GetProperty("number").GetString()).Should().Equal("101", "102");
        items.Select(item => item.GetProperty("balance").GetDecimal()).Should().Equal(600m, 400m);
        JsonElement debtor = items[0];
        debtor.GetProperty("unitId").GetGuid().Should().NotBeEmpty();
        debtor.GetProperty("block").ValueKind.Should().Be(JsonValueKind.Null);
        debtor.GetProperty("overdueCharges").GetInt32().Should().Be(1);
        debtor.GetProperty("oldestDueDate").GetString().Should().Be($"{month}-01");
        debtor.GetProperty("daysOverdue").GetInt32().Should().BeGreaterThan(60);
        JsonElement charge = debtor.GetProperty("charges").EnumerateArray().Single();
        charge.GetProperty("chargeId").GetGuid().Should().NotBeEmpty();
        charge.GetProperty("month").GetString().Should().Be(month);
        charge.GetProperty("dueDate").GetString().Should().Be($"{month}-01");
        charge.GetProperty("amount").GetDecimal().Should().Be(600m);
        charge.GetProperty("paid").GetDecimal().Should().Be(0m);
        charge.GetProperty("balance").GetDecimal().Should().Be(600m);
        charge.GetProperty("daysOverdue").GetInt32().Should().Be(debtor.GetProperty("daysOverdue").GetInt32());
    }

    [Fact]
    public async Task A_payment_lowers_the_debt_and_paying_everything_takes_the_unit_off_the_list()
    {
        ManagerSession manager = await RegisterAsync();
        string month = Month(-3);
        await ClosedMonthKit.CloseAsync(_client, manager, month, $"{month}-01");
        Guid first = await FirstChargeAsync(manager);

        await PayAsync(manager, first, 250m);
        JsonElement partial = await ReadAsync(await ListAsync(manager));

        partial.GetProperty("totalOverdue").GetDecimal().Should().Be(750m);
        JsonElement[] items = [.. partial.GetProperty("items").EnumerateArray()];
        items.Select(item => item.GetProperty("number").GetString()).Should().Equal("102", "101");
        JsonElement debtor = items[1];
        debtor.GetProperty("balance").GetDecimal().Should().Be(350m);
        debtor.GetProperty("charges")[0].GetProperty("paid").GetDecimal().Should().Be(250m);

        await PayAsync(manager, first, 350m);
        JsonElement settled = await ReadAsync(await ListAsync(manager));

        settled.GetProperty("units").GetInt32().Should().Be(1);
        settled.GetProperty("items")[0].GetProperty("number").GetString().Should().Be("102");
    }

    [Fact]
    public async Task A_reversed_payment_makes_the_unit_a_debtor_again()
    {
        ManagerSession manager = await RegisterAsync();
        string month = Month(-3);
        await ClosedMonthKit.CloseAsync(_client, manager, month, $"{month}-01");
        Guid first = await FirstChargeAsync(manager);
        Guid payment = await PayAsync(manager, first, 600m);
        (await ReadAsync(await ListAsync(manager))).GetProperty("units").GetInt32().Should().Be(1);

        HttpResponseMessage reversal = await _client.SendJsonAsync(
            HttpMethod.Post, $"/api/finance/payments/{payment}/reverse", manager.AccessToken, new JsonObject { ["reason"] = "Pagamento duplicado" });
        reversal.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadAsync(await ListAsync(manager))).GetProperty("units").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task The_list_paginates_and_the_totals_cover_every_debtor()
    {
        ManagerSession manager = await RegisterAsync();
        string month = Month(-3);
        await ClosedMonthKit.CloseAsync(_client, manager, month, $"{month}-01");

        JsonElement second = await ReadAsync(await ListAsync(manager, "page=2&pageSize=1"));

        second.GetProperty("page").GetInt32().Should().Be(2);
        second.GetProperty("pageSize").GetInt32().Should().Be(1);
        second.GetProperty("units").GetInt32().Should().Be(2);
        second.GetProperty("totalOverdue").GetDecimal().Should().Be(1000m);
        second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("number").GetString()).Should().Equal("102");
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    public async Task A_page_or_size_out_of_range_is_a_bad_request(string query)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await ListAsync(manager, query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("finance.delinquency.page_invalid");
    }

    [Fact]
    public async Task Another_condominium_never_sees_the_debtors()
    {
        ManagerSession owner = await RegisterAsync();
        string month = Month(-3);
        await ClosedMonthKit.CloseAsync(_client, owner, month, $"{month}-01");
        ManagerSession stranger = await ManagerSetup.RegisterAsync(fixture, _client);

        JsonElement body = await ReadAsync(await ListAsync(stranger));

        body.GetProperty("units").GetInt32().Should().Be(0);
        body.GetProperty("totalOverdue").GetDecimal().Should().Be(0m);
        (await ReadAsync(await ListAsync(owner))).GetProperty("units").GetInt32().Should().Be(2);
    }

    // ---- Apoio ----

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private Task<HttpResponseMessage> ListAsync(ManagerSession manager, string query = "") =>
        _client.SendJsonAsync(HttpMethod.Get, query.Length == 0 ? Route : $"{Route}?{query}", manager.AccessToken);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> FirstChargeAsync(ManagerSession manager)
    {
        JsonElement body = await ReadAsync(await ListAsync(manager));

        return body.GetProperty("items")[0].GetProperty("charges")[0].GetProperty("chargeId").GetGuid();
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
