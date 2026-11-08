using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Finance.Infrastructure;

namespace Urban.IntegrationTests.Api;

/// <summary>As rotas de configuração do consumo: base do fundo de reserva, percentual do fundo e tarifas de água, esgoto e gás.</summary>
[Collection(ApiCollection.Name)]
public sealed class FinanceConsumptionEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Groups = "/api/finance/expense-groups";
    private const string Settings = "/api/finance/settings";
    private const string Tariffs = "/api/finance/consumption-tariffs";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "PUT", $"{Groups}/{Guid.Empty}/reserve-fund-base" },
        { "GET", Settings },
        { "PUT", Settings },
        { "GET", Tariffs },
        { "PUT", $"{Tariffs}/2026-01" },
        { "DELETE", $"{Tariffs}/2026-01" },
    };

    private static JsonObject WaterBands() => new()
    {
        ["water"] = new JsonArray(
            new JsonObject { ["upToCubicMeters"] = 10m, ["fixedAmount"] = 47.43m, ["pricePerCubicMeter"] = 0m },
            new JsonObject { ["upToCubicMeters"] = 30m, ["fixedAmount"] = 0m, ["pricePerCubicMeter"] = 8.63m },
            new JsonObject { ["upToCubicMeters"] = null, ["fixedAmount"] = 0m, ["pricePerCubicMeter"] = 11m }),
    };

    private static JsonObject FullTariff(decimal? sewagePercent = 100m)
    {
        JsonObject body = WaterBands();
        body["sewage"] = new JsonArray(
            new JsonObject { ["upToCubicMeters"] = 10m, ["fixedAmount"] = 52.63m, ["pricePerCubicMeter"] = 0m },
            new JsonObject { ["upToCubicMeters"] = null, ["fixedAmount"] = 0m, ["pricePerCubicMeter"] = 9.635m });
        body["sewagePercent"] = sewagePercent;
        body["gas"] = new JsonObject { ["kilosPerCubicMeter"] = 2.4m, ["pricePerKilo"] = 15.71m };

        return body;
    }

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

    // ---- Base do fundo de reserva ----

    [Fact]
    public async Task The_default_group_starts_as_part_of_the_reserve_fund_base()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Groups}/default", manager.AccessToken, new JsonObject());

        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reserveFundBase").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_new_group_starts_outside_the_reserve_fund_base_and_the_listing_shows_the_flag()
    {
        ManagerSession manager = await RegisterAsync();
        await CreateGroupAsync(manager, "Garagens");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Groups, manager.AccessToken);

        (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single().GetProperty("reserveFundBase").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Put_marks_and_unmarks_the_group_and_returns_the_new_version()
    {
        ManagerSession manager = await RegisterAsync();
        JsonElement group = await CreateGroupAsync(manager, "Garagens");
        string route = $"{Groups}/{group.GetProperty("id").GetGuid()}/reserve-fund-base";

        HttpResponseMessage on = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = group.GetProperty("version").GetString(), ["enabled"] = true });
        JsonElement marked = await on.Content.ReadFromJsonAsync<JsonElement>();
        HttpResponseMessage off = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = marked.GetProperty("version").GetString(), ["enabled"] = false });

        on.StatusCode.Should().Be(HttpStatusCode.OK);
        marked.GetProperty("reserveFundBase").GetBoolean().Should().BeTrue();
        marked.GetProperty("version").GetString().Should().NotBe(group.GetProperty("version").GetString());
        on.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reserveFundBase").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Put_with_a_stale_or_missing_version_a_missing_flag_or_an_unknown_group_is_refused()
    {
        ManagerSession manager = await RegisterAsync();
        JsonElement group = await CreateGroupAsync(manager, "Garagens");
        string route = $"{Groups}/{group.GetProperty("id").GetGuid()}/reserve-fund-base";
        string version = group.GetProperty("version").GetString()!;
        await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["enabled"] = true });

        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version, ["enabled"] = false });
        HttpResponseMessage noVersion = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["enabled"] = false });
        HttpResponseMessage noFlag = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, new JsonObject { ["version"] = version });
        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Put, $"{Groups}/{Guid.NewGuid()}/reserve-fund-base", manager.AccessToken, new JsonObject { ["version"] = "1", ["enabled"] = true });

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        noVersion.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noVersion.CodeAsync()).Should().Be("finance.version.required");
        noFlag.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noFlag.CodeAsync()).Should().Be("request.invalid");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.CodeAsync()).Should().Be("expense_group.not_found");
    }

    // ---- Percentual do fundo de reserva ----

    [Fact]
    public async Task Get_settings_before_anything_is_configured_answers_zero_and_never_caches()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Settings, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reserveFundPercent").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Put_settings_saves_the_percentage_and_get_returns_it()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage saved = await _client.SendJsonAsync(HttpMethod.Put, Settings, manager.AccessToken, new JsonObject { ["reserveFundPercent"] = 15m });
        HttpResponseMessage changed = await _client.SendJsonAsync(HttpMethod.Put, Settings, manager.AccessToken, new JsonObject { ["reserveFundPercent"] = 12.5m });
        HttpResponseMessage read = await _client.SendJsonAsync(HttpMethod.Get, Settings, manager.AccessToken);

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reserveFundPercent").GetDecimal().Should().Be(15m);
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reserveFundPercent").GetDecimal().Should().Be(12.5m);
    }

    [Theory]
    [InlineData(-1, "consumption.reserve_percent_invalid")]
    [InlineData(100.5, "consumption.reserve_percent_invalid")]
    [InlineData(12.345, "consumption.reserve_percent_invalid")]
    public async Task Put_settings_with_an_invalid_percentage_answers_400(double percent, string code)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, Settings, manager.AccessToken, new JsonObject { ["reserveFundPercent"] = (decimal)percent });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Put_settings_without_the_percentage_answers_400()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, Settings, manager.AccessToken, new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("request.invalid");
    }

    // ---- Tarifas ----

    [Fact]
    public async Task Get_tariffs_before_any_is_saved_answers_an_empty_list()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Tariffs, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Put_a_full_tariff_returns_the_bands_the_sewage_and_the_gas_with_the_price_per_cubic_meter()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, FullTariff(50m));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("validFrom").GetString().Should().Be("2026-01");
        JsonElement water = body.GetProperty("water");
        water.GetArrayLength().Should().Be(3);
        water[0].GetProperty("upToCubicMeters").GetDecimal().Should().Be(10m);
        water[0].GetProperty("fixedAmount").GetDecimal().Should().Be(47.43m);
        water[1].GetProperty("pricePerCubicMeter").GetDecimal().Should().Be(8.63m);
        water[2].GetProperty("upToCubicMeters").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("sewage").GetArrayLength().Should().Be(2);
        body.GetProperty("sewagePercent").GetDecimal().Should().Be(50m);
        body.GetProperty("gas").GetProperty("kilosPerCubicMeter").GetDecimal().Should().Be(2.4m);
        body.GetProperty("gas").GetProperty("pricePerKilo").GetDecimal().Should().Be(15.71m);
        body.GetProperty("gas").GetProperty("pricePerCubicMeter").GetDecimal().Should().Be(37.70m);
    }

    [Fact]
    public async Task Put_a_water_only_tariff_defaults_the_sewage_percentage_to_100_and_has_no_sewage_or_gas()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, WaterBands());

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("sewage").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("gas").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("sewagePercent").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task Put_again_for_the_same_month_replaces_it_and_a_new_month_adds_another_listed_newest_first()
    {
        ManagerSession manager = await RegisterAsync();
        await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, FullTariff(100m));
        await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, FullTariff(50m));
        await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2025-01", manager.AccessToken, WaterBands());

        JsonElement list = await (await _client.SendJsonAsync(HttpMethod.Get, Tariffs, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();

        list.EnumerateArray().Select(item => item.GetProperty("validFrom").GetString()).Should().Equal("2026-01", "2025-01");
        list[0].GetProperty("sewagePercent").GetDecimal().Should().Be(50m);
    }

    [Theory]
    [InlineData("2026-13", "month.invalid")]
    [InlineData("janeiro", "month.invalid")]
    public async Task Put_with_an_invalid_month_answers_400(string month, string code)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/{month}", manager.AccessToken, WaterBands());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Put_with_invalid_bands_gas_or_sewage_percentage_answers_400_with_the_field_code()
    {
        ManagerSession manager = await RegisterAsync();
        JsonObject noWater = new();
        JsonObject unordered = new() { ["water"] = new JsonArray(new JsonObject { ["upToCubicMeters"] = 30m }, new JsonObject { ["upToCubicMeters"] = 10m }, new JsonObject { ["upToCubicMeters"] = null }) };
        JsonObject badGas = FullTariff();
        badGas["gas"] = new JsonObject { ["kilosPerCubicMeter"] = 0m, ["pricePerKilo"] = 15.71m };

        HttpResponseMessage missingWater = await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, noWater);
        HttpResponseMessage badBands = await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, unordered);
        HttpResponseMessage gas = await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, badGas);
        HttpResponseMessage percent = await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, FullTariff(120m));

        foreach (HttpResponseMessage response in new[] { missingWater, badBands, gas, percent })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        (await missingWater.CodeAsync()).Should().Be("consumption.bands_invalid");
        (await badBands.CodeAsync()).Should().Be("consumption.bands_invalid");
        (await gas.CodeAsync()).Should().Be("consumption.gas_invalid");
        (await percent.CodeAsync()).Should().Be("consumption.sewage_percent_invalid");
    }

    [Fact]
    public async Task Delete_removes_the_tariff_and_a_month_without_one_is_404()
    {
        ManagerSession manager = await RegisterAsync();
        await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", manager.AccessToken, WaterBands());

        HttpResponseMessage removed = await _client.SendJsonAsync(HttpMethod.Delete, $"{Tariffs}/2026-01", manager.AccessToken);
        HttpResponseMessage again = await _client.SendJsonAsync(HttpMethod.Delete, $"{Tariffs}/2026-01", manager.AccessToken);
        JsonElement list = await (await _client.SendJsonAsync(HttpMethod.Get, Tariffs, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await again.CodeAsync()).Should().Be("consumption.tariff_not_found");
        list.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task One_condominium_never_sees_the_tariffs_or_the_percentage_of_another()
    {
        ManagerSession first = await RegisterAsync();
        await _client.SendJsonAsync(HttpMethod.Put, $"{Tariffs}/2026-01", first.AccessToken, WaterBands());
        await _client.SendJsonAsync(HttpMethod.Put, Settings, first.AccessToken, new JsonObject { ["reserveFundPercent"] = 15m });
        ManagerSession second = await ManagerSetup.RegisterAsync(fixture, _client);

        JsonElement tariffs = await (await _client.SendJsonAsync(HttpMethod.Get, Tariffs, second.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        JsonElement settings = await (await _client.SendJsonAsync(HttpMethod.Get, Settings, second.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        HttpResponseMessage delete = await _client.SendJsonAsync(HttpMethod.Delete, $"{Tariffs}/2026-01", second.AccessToken);

        tariffs.GetArrayLength().Should().Be(0);
        settings.GetProperty("reserveFundPercent").GetDecimal().Should().Be(0m);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private async Task<JsonElement> CreateGroupAsync(ManagerSession manager, string name)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Groups, manager.AccessToken, new JsonObject { ["name"] = name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
