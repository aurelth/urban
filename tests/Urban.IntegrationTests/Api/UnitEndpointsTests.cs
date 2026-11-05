using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>As rotas de unidades do síndico, de ponta a ponta: acesso, validação, conflitos, versão, lote, resumo e rateio.</summary>
[Collection(ApiCollection.Name)]
public sealed class UnitEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Units = "/api/units";
    private const string Summary = "/api/units/summary";
    private const string Batch = "/api/units/batch";
    private const string Apportionment = "/api/units/apportionment-method";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    // ---- Acesso ----

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", Units },
        { "GET", Summary },
        { "GET", $"{Units}/{Guid.Empty}" },
        { "POST", Units },
        { "PUT", $"{Units}/{Guid.Empty}" },
        { "POST", $"{Units}/{Guid.Empty}/deactivate" },
        { "POST", $"{Units}/{Guid.Empty}/reactivate" },
        { "POST", Batch },
        { "PUT", Apportionment },
    };

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
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: role);
        string token = await _client.SignInAsync(user);

        foreach ((string method, string route) in new[] { ("GET", Units), ("GET", Summary), ("POST", Units), ("POST", Batch), ("PUT", Apportionment) })
        {
            HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, token, method == "GET" ? null : new JsonObject());

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");
            (await response.CodeAsync()).Should().Be("identity.user.not_manager");
        }
    }

    [Fact]
    public async Task A_manager_without_a_condominium_gets_404()
    {
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage list = await _client.SendJsonAsync(HttpMethod.Get, Units, token);
        HttpResponseMessage create = await _client.SendJsonAsync(HttpMethod.Post, Units, token, UnitBody("101"));

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await list.CodeAsync()).Should().Be("condominium.not_found");
        create.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Cadastrar ----

    [Fact]
    public async Task Post_creates_the_unit_and_answers_201_with_the_location_and_the_unit()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, UnitBody("101", block: "Bloco A", fraction: 0.0125m, area: 85.5m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Guid id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().EndWith($"{Units}/{id}");
        body.GetProperty("block").GetString().Should().Be("Bloco A");
        body.GetProperty("number").GetString().Should().Be("101");
        body.GetProperty("type").GetString().Should().Be("apartment");
        body.GetProperty("idealFraction").GetDecimal().Should().Be(0.0125m);
        body.GetProperty("area").GetDecimal().Should().Be(85.5m);
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.GetProperty("deactivatedAt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_accepts_a_house_without_block_fraction_or_area()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, new JsonObject { ["number"] = "Casa 7", ["type"] = "house" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString().Should().Be("house");
    }

    [Theory]
    [InlineData("number", "", "unit.number.invalid")]
    [InlineData("number", "   ", "unit.number.invalid")]
    [InlineData("block", "BlocoLongoDemaisBlocoLongoDemaisXX", "unit.block.invalid")]
    [InlineData("type", "castelo", "unit.type.invalid")]
    [InlineData("type", null, "unit.type.invalid")]
    [InlineData("idealFraction", 2, "unit.ideal_fraction.invalid")]
    [InlineData("idealFraction", 0, "unit.ideal_fraction.invalid")]
    [InlineData("area", -1, "unit.area.invalid")]
    [InlineData("area", 100000, "unit.area.invalid")]
    public async Task Post_with_an_invalid_field_answers_400_with_the_code_of_the_field(string field, object? value, string code)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject body = UnitBody("101");
        body[field] = JsonSerializer.SerializeToNode(value);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Post_of_a_repeated_identifier_is_409_and_a_sum_above_one_hundred_percent_is_422()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        (await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, UnitBody("101", fraction: 0.7m))).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage repeated = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, UnitBody("101"));
        HttpResponseMessage tooMuch = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, UnitBody("102", fraction: 0.4m));

        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repeated.CodeAsync()).Should().Be("unit.identifier.already_exists");
        tooMuch.StatusCode.Should().Be((HttpStatusCode)422);
        (await tooMuch.CodeAsync()).Should().Be("unit.ideal_fraction.sum_exceeded");
    }

    // ---- Consultar ----

    [Fact]
    public async Task Get_lists_the_active_units_in_natural_order_by_default_with_the_total_and_forbids_caching()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        foreach (string number in new[] { "10", "2", "1" })
        {
            await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, UnitBody(number));
        }

        JsonElement gone = await CreateAsync(manager, "3");
        await _client.SendJsonAsync(HttpMethod.Post, $"{Units}/{gone.GetProperty("id").GetGuid()}/deactivate", manager.AccessToken, new JsonObject { ["version"] = gone.GetProperty("version").GetString() });

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Units, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("number").GetString()).Should().Equal("1", "2", "10");
        body.GetProperty("total").GetInt32().Should().Be(3);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task Get_filters_by_status_type_block_and_text_and_paginates()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        await CreateAsync(manager, "101", "Bloco A");
        await CreateAsync(manager, "102", "Bloco B");
        await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, new JsonObject { ["number"] = "Casa 1", ["type"] = "house" });
        JsonElement gone = await CreateAsync(manager, "103", "Bloco A");
        await _client.SendJsonAsync(HttpMethod.Post, $"{Units}/{gone.GetProperty("id").GetGuid()}/deactivate", manager.AccessToken, new JsonObject { ["version"] = gone.GetProperty("version").GetString() });

        async Task<string[]> Numbers(string query)
        {
            JsonElement body = await (await _client.SendJsonAsync(HttpMethod.Get, $"{Units}{query}", manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
            return [.. body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("number").GetString()!)];
        }

        // A ordem é a do bloco (sem bloco primeiro) e depois a do número.
        (await Numbers("?status=all")).Should().Equal("Casa 1", "101", "103", "102");
        (await Numbers("?status=deactivated")).Should().Equal("103");
        (await Numbers("?type=house")).Should().Equal("Casa 1");
        (await Numbers("?block=Bloco%20A&status=all")).Should().Equal("101", "103");
        (await Numbers("?search=102")).Should().Equal("102");
        (await Numbers("?status=all&page=2&pageSize=3")).Should().Equal("102");
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=201")]
    public async Task Get_with_a_page_or_a_size_out_of_range_answers_400(string query)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Units}{query}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("unit.page.invalid");
    }

    [Fact]
    public async Task Get_of_a_unit_of_another_condominium_is_404_and_not_403_so_it_does_not_reveal_that_it_exists()
    {
        ManagerSession owner = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession stranger = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid id = (await CreateAsync(owner, "101")).GetProperty("id").GetGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Units}/{id}", stranger.AccessToken);
        HttpResponseMessage edit = await _client.SendJsonAsync(HttpMethod.Put, $"{Units}/{id}", stranger.AccessToken, UnitBody("999", version: "1"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("unit.not_found");
        edit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendJsonAsync(HttpMethod.Get, $"{Units}/{id}", owner.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Editar, desativar, reativar ----

    [Fact]
    public async Task Put_without_the_version_is_400_and_with_an_old_one_is_409()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement unit = await CreateAsync(manager, "101");
        string route = $"{Units}/{unit.GetProperty("id").GetGuid()}";
        string version = unit.GetProperty("version").GetString()!;

        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, UnitBody("101", area: 60m));
        HttpResponseMessage changed = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, UnitBody("101", area: 60m, version: version));
        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, UnitBody("101", area: 70m, version: version));

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.CodeAsync()).Should().Be("unit.version.required");
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement updated = await changed.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("area").GetDecimal().Should().Be(60m);
        updated.GetProperty("version").GetString().Should().NotBe(version);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
    }

    [Fact]
    public async Task Deactivate_and_reactivate_answer_with_the_unit_and_its_new_version()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement unit = await CreateAsync(manager, "101");
        string route = $"{Units}/{unit.GetProperty("id").GetGuid()}";

        HttpResponseMessage deactivated = await _client.SendJsonAsync(HttpMethod.Post, $"{route}/deactivate", manager.AccessToken, new JsonObject { ["version"] = unit.GetProperty("version").GetString() });
        JsonElement off = await deactivated.Content.ReadFromJsonAsync<JsonElement>();
        HttpResponseMessage again = await _client.SendJsonAsync(HttpMethod.Post, $"{route}/deactivate", manager.AccessToken, new JsonObject { ["version"] = off.GetProperty("version").GetString() });
        HttpResponseMessage reactivated = await _client.SendJsonAsync(HttpMethod.Post, $"{route}/reactivate", manager.AccessToken, new JsonObject { ["version"] = off.GetProperty("version").GetString() });
        JsonElement on = await reactivated.Content.ReadFromJsonAsync<JsonElement>();

        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        off.GetProperty("isActive").GetBoolean().Should().BeFalse();
        off.GetProperty("deactivatedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        off.GetProperty("version").GetString().Should().NotBe(unit.GetProperty("version").GetString());
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.CodeAsync()).Should().Be("unit.already_deactivated");
        reactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        on.GetProperty("isActive").GetBoolean().Should().BeTrue();
        on.GetProperty("deactivatedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Editing_a_deactivated_unit_is_409()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement unit = await CreateAsync(manager, "101");
        string route = $"{Units}/{unit.GetProperty("id").GetGuid()}";
        JsonElement off = await (await _client.SendJsonAsync(HttpMethod.Post, $"{route}/deactivate", manager.AccessToken, new JsonObject { ["version"] = unit.GetProperty("version").GetString() })).Content.ReadFromJsonAsync<JsonElement>();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, route, manager.AccessToken, UnitBody("102", version: off.GetProperty("version").GetString()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("unit.deactivated");
    }

    // ---- Lote ----

    [Fact]
    public async Task Batch_creates_every_unit_and_answers_201_with_the_count()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Batch, manager.AccessToken, BatchBody("301", "302", "401"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32().Should().Be(3);
        JsonElement list = await (await _client.SendJsonAsync(HttpMethod.Get, Units, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        list.GetProperty("total").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Batch_with_problems_answers_the_status_of_the_first_error_and_lists_the_position_and_the_code_of_each()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject body = BatchBody("301", "", "303");
        body["units"]![2]!["area"] = -4;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Batch, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("unit.number.invalid");
        JsonElement[] errors = [.. problem.GetProperty("errors").EnumerateArray()];
        errors.Select(e => (e.GetProperty("position").GetInt32(), e.GetProperty("code").GetString()))
            .Should().Equal((1, "unit.number.invalid"), (2, "unit.area.invalid"));
        (await ListTotalAsync(manager)).Should().Be(0);
    }

    [Fact]
    public async Task Batch_with_repeated_identifiers_is_409_with_the_positions()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        await CreateAsync(manager, "302");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Batch, manager.AccessToken, BatchBody("301", "302", "303"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("unit.identifier.already_exists");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("position").GetInt32()).Should().Equal(1);
        (await ListTotalAsync(manager)).Should().Be(1);
    }

    [Fact]
    public async Task Batch_that_is_empty_or_missing_is_400_and_a_sum_above_one_hundred_percent_is_422()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject heavy = BatchBody("1", "2");
        heavy["units"]![0]!["idealFraction"] = 0.6m;
        heavy["units"]![1]!["idealFraction"] = 0.6m;

        HttpResponseMessage empty = await _client.SendJsonAsync(HttpMethod.Post, Batch, manager.AccessToken, new JsonObject { ["units"] = new JsonArray() });
        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Post, Batch, manager.AccessToken, new JsonObject());
        HttpResponseMessage tooMuch = await _client.SendJsonAsync(HttpMethod.Post, Batch, manager.AccessToken, heavy);

        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await empty.CodeAsync()).Should().Be("unit.batch.size_invalid");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tooMuch.StatusCode.Should().Be((HttpStatusCode)422);
    }

    // ---- Resumo e rateio ----

    [Fact]
    public async Task Summary_returns_the_counts_the_sum_and_whether_each_method_is_ready()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        await CreateAsync(manager, "101", fraction: 0.6m, area: 50m);
        await CreateAsync(manager, "102", fraction: 0.4m);
        await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, new JsonObject { ["number"] = "Casa 1", ["type"] = "house" });

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Summary, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("active").GetInt32().Should().Be(3);
        body.GetProperty("deactivated").GetInt32().Should().Be(0);
        body.GetProperty("apartments").GetInt32().Should().Be(2);
        body.GetProperty("houses").GetInt32().Should().Be(1);
        body.GetProperty("idealFractionTotal").GetDecimal().Should().Be(1m);
        body.GetProperty("withoutIdealFraction").GetInt32().Should().Be(1);
        body.GetProperty("withoutArea").GetInt32().Should().Be(2);
        body.GetProperty("apportionmentMethod").GetString().Should().Be("idealFraction");
        body.GetProperty("apportionmentVersion").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("readiness").EnumerateArray()
            .ToDictionary(r => r.GetProperty("method").GetString()!, r => r.GetProperty("ready").GetBoolean())
            .Should().Equal(new Dictionary<string, bool> { ["idealFraction"] = false, ["equalShare"] = true, ["area"] = false });
    }

    [Fact]
    public async Task Put_apportionment_changes_the_method_with_the_version_and_refuses_an_unknown_method()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement summary = await (await _client.SendJsonAsync(HttpMethod.Get, Summary, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        string version = summary.GetProperty("apportionmentVersion").GetString()!;

        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Put, Apportionment, manager.AccessToken, new JsonObject { ["version"] = version, ["method"] = "metade" });
        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Put, Apportionment, manager.AccessToken, new JsonObject { ["method"] = "area" });
        HttpResponseMessage changed = await _client.SendJsonAsync(HttpMethod.Put, Apportionment, manager.AccessToken, new JsonObject { ["version"] = version, ["method"] = "area" });
        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, Apportionment, manager.AccessToken, new JsonObject { ["version"] = version, ["method"] = "equalShare" });

        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknown.CodeAsync()).Should().Be("condominium.apportionment_method.invalid");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.CodeAsync()).Should().Be("condominium.version.required");
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("method").GetString().Should().Be("area");
        body.GetProperty("version").GetString().Should().NotBe(version);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement after = await (await _client.SendJsonAsync(HttpMethod.Get, Summary, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        after.GetProperty("apportionmentMethod").GetString().Should().Be("area");
    }

    // ---- Fluxo completo ----

    [Fact]
    public async Task The_whole_flow_register_create_three_units_list_edit_deactivate_and_check_the_summary()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        JsonElement first = await CreateAsync(manager, "301", fraction: 0.5m);
        await CreateAsync(manager, "302", fraction: 0.3m);
        JsonElement third = await CreateAsync(manager, "401", fraction: 0.2m);
        (await ListTotalAsync(manager)).Should().Be(3);

        HttpResponseMessage edit = await _client.SendJsonAsync(HttpMethod.Put, $"{Units}/{first.GetProperty("id").GetGuid()}", manager.AccessToken,
            UnitBody("301", fraction: 0.5m, area: 90m, version: first.GetProperty("version").GetString()));
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.SendJsonAsync(HttpMethod.Post, $"{Units}/{third.GetProperty("id").GetGuid()}/deactivate", manager.AccessToken,
            new JsonObject { ["version"] = third.GetProperty("version").GetString() })).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement summary = await (await _client.SendJsonAsync(HttpMethod.Get, Summary, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("active").GetInt32().Should().Be(2);
        summary.GetProperty("deactivated").GetInt32().Should().Be(1);
        summary.GetProperty("idealFractionTotal").GetDecimal().Should().Be(0.8m);
        (await ListTotalAsync(manager)).Should().Be(2);
    }

    // ---- Apoio ----

    private static JsonObject UnitBody(string number, string? block = null, decimal? fraction = null, decimal? area = null, string? version = null) => new()
    {
        ["version"] = version,
        ["block"] = block,
        ["number"] = number,
        ["type"] = "apartment",
        ["idealFraction"] = fraction,
        ["area"] = area,
    };

    private static JsonObject BatchBody(params string[] numbers) => new()
    {
        ["units"] = new JsonArray([.. numbers.Select(n => (JsonNode)new JsonObject { ["number"] = n, ["type"] = "apartment" })]),
    };

    private async Task<JsonElement> CreateAsync(ManagerSession manager, string number, string? block = null, decimal? fraction = null, decimal? area = null)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, UnitBody(number, block, fraction, area));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<int> ListTotalAsync(ManagerSession manager) =>
        (await (await _client.SendJsonAsync(HttpMethod.Get, Units, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32();
}
