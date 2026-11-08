using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Finance.Infrastructure;

namespace Urban.IntegrationTests.Api;

/// <summary>As leituras de água e gás do mês e o fechamento que as usa, com tarifa e fundo de reserva, de ponta a ponta pela API.</summary>
[Collection(ApiCollection.Name)]
public sealed class FinanceReadingsEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Groups = "/api/finance/expense-groups";
    private const string Expenses = "/api/finance/expenses";
    private const string Months = "/api/finance/months";
    private const string Settings = "/api/finance/settings";
    private const string Tariffs = "/api/finance/consumption-tariffs";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", $"{Months}/2026-11/readings" },
        { "PUT", $"{Months}/2026-11/readings" },
    };

    private static string Month(int offset)
    {
        DateTimeOffset brasilia = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3));
        DateTime shifted = new DateTime(brasilia.Year, brasilia.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(offset);

        return shifted.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    private static JsonObject Reading(Guid unit, string service, decimal? previous, decimal? current) => new()
    {
        ["unitId"] = unit,
        ["service"] = service,
        ["previous"] = previous,
        ["current"] = current,
    };

    private static JsonObject Readings(params JsonObject[] readings) => new() { ["readings"] = new JsonArray([.. readings]) };

    private static JsonObject WaterTariff() => new()
    {
        ["water"] = new JsonArray(
            new JsonObject { ["upToCubicMeters"] = 10m, ["fixedAmount"] = 47.43m, ["pricePerCubicMeter"] = 0m },
            new JsonObject { ["upToCubicMeters"] = 30m, ["fixedAmount"] = 0m, ["pricePerCubicMeter"] = 8.63m },
            new JsonObject { ["upToCubicMeters"] = null, ["fixedAmount"] = 0m, ["pricePerCubicMeter"] = 11m }),
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
    [MemberData(nameof(AllRoutes))]
    public async Task A_resident_is_refused(string method, string route)
    {
        await MigrateAsync();
        Urban.Identity.Domain.Users.User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: Urban.Identity.Domain.Access.UserRole.Resident);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, token, method == "GET" ? null : new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    // ---- Leituras ----

    [Fact]
    public async Task Get_lists_the_units_without_readings_and_says_there_is_no_tariff()
    {
        ManagerSession manager = await RegisterAsync();
        Guid first = await CreateUnitAsync(manager, "101", 0.6m);
        Guid second = await CreateUnitAsync(manager, "102", 0.4m);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{Month(0)}/readings", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetString().Should().Be(Month(0));
        body.GetProperty("isClosed").GetBoolean().Should().BeFalse();
        body.GetProperty("hasTariff").GetBoolean().Should().BeFalse();
        body.GetProperty("hasGas").GetBoolean().Should().BeFalse();
        JsonElement[] units = [.. body.GetProperty("units").EnumerateArray()];
        units.Select(unit => unit.GetProperty("unitId").GetGuid()).Should().Equal(first, second);
        units[0].GetProperty("number").GetString().Should().Be("101");
        units[0].GetProperty("water").ValueKind.Should().Be(JsonValueKind.Null);
        units[0].GetProperty("gas").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_stores_readings_and_the_next_month_suggests_the_current_reading_as_the_previous_one()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unit = await CreateUnitAsync(manager, "101", 1m);

        HttpResponseMessage saved = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(-1)}/readings", manager.AccessToken, Readings(Reading(unit, "water", 1551.913m, 1569.562m), Reading(unit, "gas", 117.825m, 118.677m)));
        JsonElement next = await (await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{Month(0)}/readings", manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();

        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        saved.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement water = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("units")[0].GetProperty("water");
        (water.GetProperty("previous").GetDecimal(), water.GetProperty("current").GetDecimal(), water.GetProperty("consumption").GetDecimal()).Should().Be((1551.913m, 1569.562m, 17.649m));
        next.GetProperty("units")[0].GetProperty("suggestedWaterPrevious").GetDecimal().Should().Be(1569.562m);
        next.GetProperty("units")[0].GetProperty("suggestedGasPrevious").GetDecimal().Should().Be(118.677m);
    }

    [Fact]
    public async Task Put_without_the_previous_reading_uses_last_months_current_one_and_without_the_current_one_removes_it()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unit = await CreateUnitAsync(manager, "101", 1m);
        await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(-1)}/readings", manager.AccessToken, Readings(Reading(unit, "water", 10m, 20m)));

        JsonElement added = await (await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "water", null, 27.5m)))).Content.ReadFromJsonAsync<JsonElement>();
        JsonElement removed = await (await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "water", null, null)))).Content.ReadFromJsonAsync<JsonElement>();

        added.GetProperty("units")[0].GetProperty("water").GetProperty("previous").GetDecimal().Should().Be(20m);
        added.GetProperty("units")[0].GetProperty("water").GetProperty("consumption").GetDecimal().Should().Be(7.5m);
        removed.GetProperty("units")[0].GetProperty("water").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_again_corrects_the_reading_instead_of_adding_another()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unit = await CreateUnitAsync(manager, "101", 1m);
        await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "water", 10m, 20m)));

        HttpResponseMessage again = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "water", 10m, 22m)));

        again.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement units = (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("units");
        units.GetArrayLength().Should().Be(1);
        units[0].GetProperty("water").GetProperty("current").GetDecimal().Should().Be(22m);
    }

    [Fact]
    public async Task Put_refuses_the_whole_request_when_one_reading_is_invalid()
    {
        ManagerSession manager = await RegisterAsync();
        Guid first = await CreateUnitAsync(manager, "101", 0.5m);
        Guid second = await CreateUnitAsync(manager, "102", 0.5m);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(first, "water", 10m, 20m), Reading(second, "water", 50m, 40m)));
        JsonElement units = (await (await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{Month(0)}/readings", manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("units");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("consumption.reading_invalid");
        units.EnumerateArray().Should().OnlyContain(unit => unit.GetProperty("water").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_answers_the_error_of_each_wrong_request()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unit = await CreateUnitAsync(manager, "101", 1m);

        HttpResponseMessage service = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "electricity", 1m, 2m)));
        HttpResponseMessage stranger = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(Guid.NewGuid(), "water", 1m, 2m)));
        HttpResponseMessage noUnit = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(new JsonObject { ["service"] = "water", ["current"] = 2m }));
        HttpResponseMessage month = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/abril/readings", manager.AccessToken, Readings(Reading(unit, "water", 1m, 2m)));
        HttpResponseMessage getMonth = await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/abril/readings", manager.AccessToken);

        service.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await service.CodeAsync()).Should().Be("consumption.service_invalid");
        stranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.CodeAsync()).Should().Be("consumption.unit_not_found");
        noUnit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await noUnit.CodeAsync()).Should().Be("consumption.unit_not_found");
        month.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        getMonth.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_without_a_body_list_changes_nothing()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateUnitAsync(manager, "101", 1m);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("units").GetArrayLength().Should().Be(1);
    }

    // ---- Fechamento com consumo e fundo de reserva ----

    [Fact]
    public async Task Closing_charges_expenses_by_group_the_reserve_fund_and_the_water_of_each_unit()
    {
        ManagerSession manager = await RegisterAsync();
        Guid first = await CreateUnitAsync(manager, "101", 0.6m);
        Guid second = await CreateUnitAsync(manager, "102", 0.4m);
        await ConfigureAsync(manager);
        await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(first, "water", 100m, 117.649m), Reading(second, "water", 50m, 55m)));

        HttpResponseMessage response = await CloseAsync(manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement closing = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("closing");
        closing.GetProperty("total").GetDecimal().Should().Be(1000m);
        JsonElement[] charges = [.. closing.GetProperty("charges").EnumerateArray()];
        charges.Select(charge => charge.GetProperty("amount").GetDecimal()).Should().Equal(803.44m, 507.43m);
        JsonElement[] lines = [.. charges[0].GetProperty("lines").EnumerateArray()];
        lines.Select(line => (line.GetProperty("kind").GetString(), line.GetProperty("label").GetString(), line.GetProperty("amount").GetDecimal())).Should().Equal(
            ("expenses", "Contratos", 600m),
            ("reserveFund", "Fundo de reserva", 90m),
            ("water", "Água", 113.44m));
        JsonElement water = lines[2];
        (water.GetProperty("quantity").GetDecimal(), water.GetProperty("previousReading").GetDecimal(), water.GetProperty("currentReading").GetDecimal()).Should().Be((17.649m, 100m, 117.649m));
        lines[0].GetProperty("quantity").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Closing_without_the_readings_of_every_unit_is_422_and_after_they_are_informed_it_works()
    {
        ManagerSession manager = await RegisterAsync();
        Guid first = await CreateUnitAsync(manager, "101", 0.6m);
        Guid second = await CreateUnitAsync(manager, "102", 0.4m);
        await ConfigureAsync(manager);
        await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(first, "water", 100m, 110m)));

        HttpResponseMessage pending = await CloseAsync(manager);
        await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(second, "water", 50m, 55m)));
        HttpResponseMessage closed = await CloseAsync(manager);

        pending.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await pending.CodeAsync()).Should().Be("closing.readings_pending");
        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_closed_month_refuses_new_readings_and_reopening_it_allows_them_again()
    {
        ManagerSession manager = await RegisterAsync();
        Guid unit = await CreateUnitAsync(manager, "101", 1m);
        await ConfigureAsync(manager);
        await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "water", 100m, 110m)));
        await CloseAsync(manager);

        HttpResponseMessage refused = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "water", 100m, 120m)));
        JsonElement view = await (await _client.SendJsonAsync(HttpMethod.Get, $"{Months}/{Month(0)}/readings", manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/reopen", manager.AccessToken, new JsonObject());
        HttpResponseMessage allowed = await _client.SendJsonAsync(HttpMethod.Put, $"{Months}/{Month(0)}/readings", manager.AccessToken, Readings(Reading(unit, "water", 100m, 120m)));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.CodeAsync()).Should().Be("closing.month_is_closed");
        view.GetProperty("isClosed").GetBoolean().Should().BeTrue();
        view.GetProperty("hasTariff").GetBoolean().Should().BeTrue();
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Apoio ----

    // Um grupo "Contratos" na base do fundo (15%), uma despesa fixa de R$ 1.000 no mês e a tarifa de água por faixas (sem esgoto nem gás).
    private async Task ConfigureAsync(ManagerSession manager)
    {
        JsonElement group = await CreateGroupAsync(manager, "Contratos");
        await _client.SendJsonAsync(HttpMethod.Put, $"{Groups}/{group.GetProperty("id").GetGuid()}/reserve-fund-base", manager.AccessToken, new JsonObject { ["version"] = group.GetProperty("version").GetString(), ["enabled"] = true });
        await _client.SendJsonAsync(HttpMethod.Put, Settings, manager.AccessToken, new JsonObject { ["reserveFundPercent"] = 15m });
        await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/{Month(-6)}", manager.AccessToken, WaterTariff());
        HttpResponseMessage expense = await _client.SendJsonAsync(
            HttpMethod.Post,
            Expenses,
            manager.AccessToken,
            new JsonObject { ["kind"] = "fixed", ["groupId"] = group.GetProperty("id").GetGuid(), ["description"] = "Portaria", ["startMonth"] = Month(0), ["amount"] = 1000m, ["dueDay"] = 10 });
        expense.StatusCode.Should().Be(HttpStatusCode.Created, await expense.Content.ReadAsStringAsync());
        (await _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/open", manager.AccessToken, new JsonObject())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> CloseAsync(ManagerSession manager) =>
        _client.SendJsonAsync(HttpMethod.Post, $"{Months}/{Month(0)}/close", manager.AccessToken, new JsonObject { ["dueDate"] = "2099-12-10" });

    private async Task<JsonElement> CreateGroupAsync(ManagerSession manager, string name)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Groups, manager.AccessToken, new JsonObject { ["name"] = name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
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

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }
}
