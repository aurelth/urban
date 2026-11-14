using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>As rotas de veículos do síndico, de ponta a ponta: acesso, validação, placa única, versão, exclusão, lista e isolamento entre condomínios.</summary>
[Collection(ApiCollection.Name)]
public sealed class VehicleEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Vehicles = "/api/vehicles";
    private const string Units = "/api/units";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    // ---- Acesso ----

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", Vehicles },
        { "GET", $"{Vehicles}/{Guid.Empty}" },
        { "POST", Vehicles },
        { "PUT", $"{Vehicles}/{Guid.Empty}" },
        { "DELETE", $"{Vehicles}/{Guid.Empty}" },
    };

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Every_route_requires_a_token(string method, string route)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, null, method is "POST" or "PUT" ? new JsonObject() : null);

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

        foreach ((string method, string route) in new[] { ("GET", Vehicles), ("POST", Vehicles), ("PUT", $"{Vehicles}/{Guid.Empty}"), ("DELETE", $"{Vehicles}/{Guid.Empty}") })
        {
            HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, token, method is "POST" or "PUT" ? new JsonObject() : null);

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

        HttpResponseMessage list = await _client.SendJsonAsync(HttpMethod.Get, Vehicles, token);
        HttpResponseMessage create = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, token, VehicleBody(Guid.CreateVersion7(), "ABC1D23"));

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await list.CodeAsync()).Should().Be("condominium.not_found");
        create.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Cadastrar ----

    [Fact]
    public async Task Post_registers_the_vehicle_and_answers_201_with_the_location_and_the_vehicle()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101", "Bloco A");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, VehicleBody(unitId, " abc-1d23 ", " Fiat  Argo ", "Prata"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Guid id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().EndWith($"{Vehicles}/{id}");
        body.GetProperty("plate").GetString().Should().Be("ABC1D23");
        body.GetProperty("model").GetString().Should().Be("Fiat Argo");
        body.GetProperty("color").GetString().Should().Be("Prata");
        body.GetProperty("unitId").GetGuid().Should().Be(unitId);
        body.GetProperty("unitBlock").GetString().Should().Be("Bloco A");
        body.GetProperty("unitNumber").GetString().Should().Be("101");
        body.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_accepts_the_old_plate_format()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, VehicleBody(unitId, "abc-1234"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("plate").GetString().Should().Be("ABC1234");
    }

    [Theory]
    [InlineData("plate", "ABC12", "vehicle.plate.invalid")]
    [InlineData("plate", "", "vehicle.plate.invalid")]
    [InlineData("plate", null, "vehicle.plate.invalid")]
    [InlineData("model", "", "vehicle.model.invalid")]
    [InlineData("model", "G", "vehicle.model.invalid")]
    [InlineData("color", "", "vehicle.color.invalid")]
    [InlineData("color", "AzulAzulAzulAzulAzulAzulAzulAzul", "vehicle.color.invalid")]
    public async Task Post_with_an_invalid_field_answers_400_with_the_code_of_the_field(string field, string? value, string code)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject body = VehicleBody(await CreateUnitAsync(manager, "101"), "ABC1D23");
        body[field] = value;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Post_without_a_unit_answers_400()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject body = VehicleBody(Guid.CreateVersion7(), "ABC1D23");
        body.Remove("unitId");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("vehicle.unit_required");
    }

    [Fact]
    public async Task Post_with_a_unit_that_does_not_exist_answers_404()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, VehicleBody(Guid.CreateVersion7(), "ABC1D23"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("unit.not_found");
    }

    [Fact]
    public async Task Post_with_a_unit_of_another_condominium_answers_404_as_if_it_did_not_exist()
    {
        ManagerSession mine = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid othersUnit = await CreateUnitAsync(other, "101");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, mine.AccessToken, VehicleBody(othersUnit, "ABC1D23"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("unit.not_found");
    }

    [Fact]
    public async Task Post_with_a_deactivated_unit_answers_409()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        await DeactivateUnitAsync(manager, unitId);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, VehicleBody(unitId, "ABC1D23"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("unit.deactivated");
    }

    [Fact]
    public async Task Post_with_a_plate_already_registered_answers_409_even_when_typed_differently()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        await CreateAsync(manager, unitId, "ABC1234");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, VehicleBody(unitId, "abc-1234"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("vehicle.plate.already_registered");
    }

    [Fact]
    public async Task The_same_plate_can_be_registered_in_two_condominiums()
    {
        ManagerSession a = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession b = await ManagerSetup.RegisterAsync(fixture, _client);

        await CreateAsync(a, await CreateUnitAsync(a, "101"), "ABC1D23");
        HttpResponseMessage second = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, b.AccessToken, VehicleBody(await CreateUnitAsync(b, "101"), "ABC1D23"));

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---- Ler ----

    [Fact]
    public async Task Get_returns_the_vehicle_with_its_version_and_never_caches()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement created = await CreateAsync(manager, await CreateUnitAsync(manager, "101", "Bloco B"), "ABC1D23");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Vehicles}/{created.GetProperty("id").GetGuid()}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("plate").GetString().Should().Be("ABC1D23");
        body.GetProperty("unitBlock").GetString().Should().Be("Bloco B");
        body.GetProperty("version").GetString().Should().Be(created.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Get_of_a_vehicle_from_another_condominium_answers_404_as_if_it_did_not_exist()
    {
        ManagerSession mine = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement othersVehicle = await CreateAsync(other, await CreateUnitAsync(other, "101"), "ABC1D23");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Vehicles}/{othersVehicle.GetProperty("id").GetGuid()}", mine.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("vehicle.not_found");
    }

    // ---- Editar ----

    [Fact]
    public async Task Put_changes_the_vehicle_and_returns_the_new_version()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unit = await CreateUnitAsync(manager, "101", "Bloco A");
        Guid otherUnit = await CreateUnitAsync(manager, "202", "Bloco B");
        JsonElement created = await CreateAsync(manager, unit, "ABC1D23");
        Guid id = created.GetProperty("id").GetGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Vehicles}/{id}", manager.AccessToken, VehicleBody(otherUnit, "xyz-9876", "Honda Civic", "Branco", created.GetProperty("version").GetString()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("plate").GetString().Should().Be("XYZ9876");
        body.GetProperty("model").GetString().Should().Be("Honda Civic");
        body.GetProperty("color").GetString().Should().Be("Branco");
        body.GetProperty("unitId").GetGuid().Should().Be(otherUnit);
        body.GetProperty("unitBlock").GetString().Should().Be("Bloco B");
        body.GetProperty("version").GetString().Should().NotBe(created.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Put_without_the_version_answers_400()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unit = await CreateUnitAsync(manager, "101");
        JsonElement created = await CreateAsync(manager, unit, "ABC1D23");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Vehicles}/{created.GetProperty("id").GetGuid()}", manager.AccessToken, VehicleBody(unit, "ABC1D23"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("vehicle.version.required");
    }

    [Fact]
    public async Task Put_with_an_old_version_answers_409_and_changes_nothing()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unit = await CreateUnitAsync(manager, "101");
        JsonElement created = await CreateAsync(manager, unit, "ABC1D23", "Gol", "Preto");
        string id = created.GetProperty("id").GetString()!;
        string oldVersion = created.GetProperty("version").GetString()!;
        (await _client.SendJsonAsync(HttpMethod.Put, $"{Vehicles}/{id}", manager.AccessToken, VehicleBody(unit, "ABC1D23", "Gol", "Vermelho", oldVersion))).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, $"{Vehicles}/{id}", manager.AccessToken, VehicleBody(unit, "ABC1D23", "Gol", "Azul", oldVersion));

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        JsonElement current = await (await _client.SendJsonAsync(HttpMethod.Get, $"{Vehicles}/{id}", manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        current.GetProperty("color").GetString().Should().Be("Vermelho");
    }

    [Fact]
    public async Task Put_with_nothing_changed_answers_200_and_keeps_the_version()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unit = await CreateUnitAsync(manager, "101");
        JsonElement created = await CreateAsync(manager, unit, "ABC1D23", "Fiat Argo", "Prata");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Vehicles}/{created.GetProperty("id").GetGuid()}", manager.AccessToken, VehicleBody(unit, "abc1d23", "Fiat Argo", "Prata", created.GetProperty("version").GetString()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString().Should().Be(created.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Put_with_the_plate_of_another_vehicle_answers_409()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unit = await CreateUnitAsync(manager, "101");
        await CreateAsync(manager, unit, "AAA1111");
        JsonElement second = await CreateAsync(manager, unit, "BBB2222");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Vehicles}/{second.GetProperty("id").GetGuid()}", manager.AccessToken, VehicleBody(unit, "AAA1111", version: second.GetProperty("version").GetString()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("vehicle.plate.already_registered");
    }

    [Fact]
    public async Task Put_of_a_vehicle_from_another_condominium_answers_404_and_changes_nothing()
    {
        ManagerSession mine = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid othersUnit = await CreateUnitAsync(other, "101");
        JsonElement othersVehicle = await CreateAsync(other, othersUnit, "ABC1D23", "Gol", "Preto");
        Guid myUnit = await CreateUnitAsync(mine, "101");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Vehicles}/{othersVehicle.GetProperty("id").GetGuid()}", mine.AccessToken, VehicleBody(myUnit, "ABC1D23", "Gol", "Rosa", othersVehicle.GetProperty("version").GetString()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("vehicle.not_found");
        JsonElement current = await (await _client.SendJsonAsync(HttpMethod.Get, $"{Vehicles}/{othersVehicle.GetProperty("id").GetGuid()}", other.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        current.GetProperty("color").GetString().Should().Be("Preto");
    }

    // ---- Excluir ----

    [Fact]
    public async Task Delete_removes_the_vehicle_and_frees_the_plate()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unit = await CreateUnitAsync(manager, "101");
        JsonElement created = await CreateAsync(manager, unit, "ABC1D23");
        string route = $"{Vehicles}/{created.GetProperty("id").GetGuid()}";

        HttpResponseMessage deleted = await _client.SendJsonAsync(HttpMethod.Delete, route, manager.AccessToken);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.SendJsonAsync(HttpMethod.Get, route, manager.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendJsonAsync(HttpMethod.Delete, route, manager.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, VehicleBody(unit, "ABC1D23"))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Delete_of_a_vehicle_from_another_condominium_answers_404_and_the_vehicle_stays()
    {
        ManagerSession mine = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement othersVehicle = await CreateAsync(other, await CreateUnitAsync(other, "101"), "ABC1D23");
        string route = $"{Vehicles}/{othersVehicle.GetProperty("id").GetGuid()}";

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Delete, route, mine.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("vehicle.not_found");
        (await _client.SendJsonAsync(HttpMethod.Get, route, other.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Lista ----

    [Fact]
    public async Task Get_lists_only_the_vehicles_of_the_condominium_in_unit_order_with_the_total_and_never_caches()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid ten = await CreateUnitAsync(manager, "10", "Bloco A");
        Guid two = await CreateUnitAsync(manager, "2", "Bloco A");
        await CreateAsync(manager, ten, "CCC3333");
        await CreateAsync(manager, two, "BBB2222");
        await CreateAsync(other, await CreateUnitAsync(other, "1"), "ZZZ9999");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Vehicles, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(2);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(50);
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("plate").GetString()).Should().Equal("BBB2222", "CCC3333");
    }

    [Fact]
    public async Task Get_filters_by_unit_and_by_search_and_pages()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid first = await CreateUnitAsync(manager, "101");
        Guid second = await CreateUnitAsync(manager, "102");
        await CreateAsync(manager, first, "AAA1111", "Gol", "Preto");
        await CreateAsync(manager, first, "BBB2222", "Fiat Argo", "Prata");
        await CreateAsync(manager, second, "CCC3333", "Honda Civic", "Branco");

        JsonElement byUnit = await ListAsync(manager, $"unitId={second}");
        JsonElement bySearch = await ListAsync(manager, "search=argo");
        JsonElement byPlate = await ListAsync(manager, "search=ccc-33");
        JsonElement paged = await ListAsync(manager, "page=2&pageSize=2");

        byUnit.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("plate").GetString()).Should().Equal("CCC3333");
        bySearch.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("plate").GetString()).Should().Equal("BBB2222");
        byPlate.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("plate").GetString()).Should().Equal("CCC3333");
        paged.GetProperty("total").GetInt32().Should().Be(3);
        paged.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("plate").GetString()).Should().Equal("CCC3333");
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    public async Task Get_with_a_page_outside_the_limits_answers_400(string query)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Vehicles}?{query}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("vehicle.page.invalid");
    }

    // ---- Apoio ----

    private static JsonObject VehicleBody(Guid unitId, string? plate, string? model = "Fiat Argo", string? color = "Prata", string? version = null) => new()
    {
        ["version"] = version,
        ["unitId"] = unitId,
        ["plate"] = plate,
        ["model"] = model,
        ["color"] = color,
    };

    private async Task<Guid> CreateUnitAsync(ManagerSession manager, string number, string? block = null)
    {
        JsonObject body = new() { ["block"] = block, ["number"] = number, ["type"] = "apartment" };
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task DeactivateUnitAsync(ManagerSession manager, Guid unitId)
    {
        JsonElement unit = await (await _client.SendJsonAsync(HttpMethod.Get, $"{Units}/{unitId}", manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post, $"{Units}/{unitId}/deactivate", manager.AccessToken, new JsonObject { ["version"] = unit.GetProperty("version").GetString() });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<JsonElement> CreateAsync(ManagerSession manager, Guid unitId, string plate, string model = "Fiat Argo", string color = "Prata")
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Vehicles, manager.AccessToken, VehicleBody(unitId, plate, model, color));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ListAsync(ManagerSession manager, string query)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Vehicles}?{query}", manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
