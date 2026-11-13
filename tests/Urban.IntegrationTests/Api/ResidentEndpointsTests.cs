using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>As rotas de moradores do síndico, de ponta a ponta: acesso, validação, conflitos, isolamento entre condomínios, lista e leitura.</summary>
[Collection(ApiCollection.Name)]
public sealed class ResidentEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Residents = "/api/residents";
    private const string Units = "/api/units";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    // ---- Acesso ----

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", Residents },
        { "GET", $"{Residents}/{Guid.Empty}" },
        { "POST", Residents },
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

        foreach ((string method, string route) in new[] { ("GET", Residents), ("GET", $"{Residents}/{Guid.NewGuid()}"), ("POST", Residents) })
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

        HttpResponseMessage list = await _client.SendJsonAsync(HttpMethod.Get, Residents, token);
        HttpResponseMessage create = await _client.SendJsonAsync(HttpMethod.Post, Residents, token, ResidentBody(Guid.NewGuid()));

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await list.CodeAsync()).Should().Be("condominium.not_found");
        create.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await create.CodeAsync()).Should().Be("condominium.not_found");
    }

    // ---- Cadastrar ----

    [Fact]
    public async Task Post_registers_the_resident_and_answers_201_with_the_location_and_the_resident()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101", "Bloco A");
        string cpf = CondominiumBuilders.UniqueCpf().Value;
        string punctuated = $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
        JsonObject body = ResidentBody(unitId, cpf: punctuated, kind: "tenant");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        string text = await response.Content.ReadAsStringAsync(Ct);
        JsonElement json = JsonDocument.Parse(text).RootElement;
        Guid id = json.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().EndWith($"{Residents}/{id}");
        json.GetProperty("fullName").GetString().Should().Be("Maria Souza");
        json.GetProperty("maskedCpf").GetString().Should().Be($"***.{cpf[3..6]}.{cpf[6..9]}-**");
        json.GetProperty("birthDate").GetString().Should().Be("1990-05-20");
        json.GetProperty("phone").GetString().Should().Be("47998765432");
        json.GetProperty("email").GetString().Should().Be("maria@exemplo.com");
        json.GetProperty("profession").GetString().Should().Be("Engenheira");
        json.GetProperty("emergencyContact").GetProperty("name").GetString().Should().Be("Carlos Souza");
        json.GetProperty("emergencyContact").GetProperty("relationship").GetString().Should().Be("Irmão");
        json.GetProperty("emergencyContact").GetProperty("phone").GetString().Should().Be("4733334444");
        json.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
        JsonElement occupancy = json.GetProperty("occupancies").EnumerateArray().Single();
        occupancy.GetProperty("unitId").GetGuid().Should().Be(unitId);
        occupancy.GetProperty("unitBlock").GetString().Should().Be("Bloco A");
        occupancy.GetProperty("unitNumber").GetString().Should().Be("101");
        occupancy.GetProperty("kind").GetString().Should().Be("tenant");
        occupancy.GetProperty("startDate").GetString().Should().Be("2024-03-01");
        occupancy.GetProperty("endDate").ValueKind.Should().Be(JsonValueKind.Null);
        text.Should().NotContain(cpf).And.NotContain(punctuated);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("tenant")]
    [InlineData("OWNER")]
    public async Task Post_accepts_both_kinds(string kind)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, ResidentBody(unitId, kind: kind));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Post_without_email_registers_the_resident_without_access_data(string? email)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        JsonObject body = ResidentBody(unitId);
        body["email"] = email;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Post_does_not_create_a_user_account()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        string email = $"sem-conta-{Guid.NewGuid():N}@exemplo.com";

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, ResidentBody(unitId, email: email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        HttpResponseMessage login = await _client.LoginAsync(email, ManagerSetup.Password);
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public static TheoryData<string, object?, string> InvalidFields => new()
    {
        { "fullName", "Maria", "condominium.person.full_name_invalid" },
        { "fullName", null, "condominium.person.full_name_invalid" },
        { "cpf", "111.111.111-11", "condominium.cpf.invalid" },
        { "cpf", null, "condominium.cpf.invalid" },
        { "birthDate", null, "condominium.birth_date.invalid" },
        { "birthDate", "2999-01-01", "condominium.birth_date.invalid" },
        { "birthDate", "UNDER_AGE", "condominium.resident.under_age" },
        { "phone", "123", "condominium.phone.invalid" },
        { "email", "sem-arroba", "condominium.email.invalid" },
        { "profession", "x", "condominium.profession.invalid" },
        { "profession", null, "condominium.profession.invalid" },
        { "kind", "castelo", "condominium.occupancy.kind_invalid" },
        { "kind", null, "condominium.occupancy.kind_invalid" },
        { "startDate", null, "condominium.occupancy.start_date_invalid" },
        { "startDate", "FAR_FUTURE", "condominium.occupancy.start_date_invalid" },
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Post_with_an_invalid_field_answers_400_with_the_code_of_the_field(string field, object? value, string code)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        JsonObject body = ResidentBody(unitId);
        body[field] = value switch
        {
            "UNDER_AGE" => DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd"),
            "FAR_FUTURE" => DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2).ToString("yyyy-MM-dd"),
            _ => JsonSerializer.SerializeToNode(value),
        };

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Theory]
    [InlineData("name", "Carlos", "condominium.emergency_contact.name_invalid")]
    [InlineData("relationship", "", "condominium.emergency_contact.relationship_invalid")]
    [InlineData("phone", "1", "condominium.emergency_contact.phone_invalid")]
    public async Task Post_with_an_invalid_emergency_contact_answers_400_with_the_code_of_the_part(string part, string value, string code)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        JsonObject body = ResidentBody(unitId);
        body["emergencyContact"]![part] = value;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Post_without_the_emergency_contact_answers_400()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        JsonObject body = ResidentBody(unitId);
        body["emergencyContact"] = null;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.emergency_contact.name_invalid");
    }

    [Fact]
    public async Task Post_with_the_same_cpf_twice_answers_409_even_with_other_punctuation()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        string cpf = CondominiumBuilders.UniqueCpf().Value;
        (await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, ResidentBody(unitId, cpf: cpf))).StatusCode.Should().Be(HttpStatusCode.Created);

        string punctuated = $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
        HttpResponseMessage again = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, ResidentBody(unitId, cpf: punctuated));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.CodeAsync()).Should().Be(PersonErrors.CpfAlreadyRegistered.Code);
    }

    [Fact]
    public async Task Post_with_the_cpf_of_the_manager_answers_409()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, ResidentBody(unitId, cpf: ManagerSetup.Cpf));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_with_an_unknown_unit_answers_404()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, ResidentBody(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be(UnitErrors.NotFound.Code);
    }

    [Fact]
    public async Task Post_with_the_unit_of_another_condominium_answers_404_and_registers_nothing()
    {
        ManagerSession mine = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid foreignUnit = await CreateUnitAsync(other, "101");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, mine.AccessToken, ResidentBody(foreignUnit));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be(UnitErrors.NotFound.Code);
        (await ListAsync(mine)).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Post_with_a_deactivated_unit_answers_409()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement unit = await CreateUnitJsonAsync(manager, "101");
        await _client.SendJsonAsync(HttpMethod.Post, $"{Units}/{unit.GetProperty("id").GetGuid()}/deactivate", manager.AccessToken, new JsonObject { ["version"] = unit.GetProperty("version").GetString() });

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, ResidentBody(unit.GetProperty("id").GetGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be(UnitErrors.Deactivated.Code);
    }

    // ---- Ler e listar ----

    [Fact]
    public async Task Get_returns_the_resident_with_the_masked_cpf_and_no_cache()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101", "Bloco A");
        Guid id = await RegisterAsync(manager, unitId, "Maria Souza");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}/{id}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().Be(id);
        json.GetProperty("maskedCpf").GetString().Should().StartWith("***.").And.EndWith("-**");
        json.GetProperty("occupancies").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Get_of_an_unknown_resident_answers_404()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}/{Guid.NewGuid()}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.resident.not_found");
    }

    [Fact]
    public async Task Get_of_a_resident_of_another_condominium_is_404_and_not_403_so_it_does_not_reveal_that_it_exists()
    {
        ManagerSession mine = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid id = await RegisterAsync(other, await CreateUnitAsync(other, "101"), "Pessoa De Fora");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}/{id}", mine.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.resident.not_found");
    }

    [Fact]
    public async Task Get_of_the_manager_own_person_is_404_because_the_manager_is_not_a_resident()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement mine = await _client.ReadMineAsync(manager.AccessToken);
        Guid managerId = mine.GetProperty("manager").TryGetProperty("id", out JsonElement id) ? id.GetGuid() : Guid.NewGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}/{managerId}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_is_ordered_by_name_and_paged_and_never_cached()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101");
        foreach (string name in new[] { "Carla Dias", "Ana Lima", "Beto Rocha" })
        {
            await RegisterAsync(manager, unitId, name);
        }

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}?page=2&pageSize=2", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("total").GetInt32().Should().Be(3);
        json.GetProperty("page").GetInt32().Should().Be(2);
        json.GetProperty("pageSize").GetInt32().Should().Be(2);
        json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("fullName").GetString()).Should().Equal("Carla Dias");
    }

    [Fact]
    public async Task List_filters_by_part_of_the_name_and_by_unit()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid a = await CreateUnitAsync(manager, "101");
        Guid b = await CreateUnitAsync(manager, "102");
        await RegisterAsync(manager, a, "Maria Souza");
        await RegisterAsync(manager, b, "Maria Teresa");
        await RegisterAsync(manager, b, "João Pereira");

        JsonElement byName = await ListAsync(manager, "?search=MARIA");
        JsonElement byUnit = await ListAsync(manager, $"?unitId={b}");
        JsonElement both = await ListAsync(manager, $"?search=maria&unitId={b}");

        Names(byName).Should().Equal("Maria Souza", "Maria Teresa");
        Names(byUnit).Should().Equal("João Pereira", "Maria Teresa");
        Names(both).Should().Equal("Maria Teresa");
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=201")]
    public async Task List_with_an_invalid_page_answers_400(string query)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}{query}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.resident.page_invalid");
    }

    [Fact]
    public async Task List_shows_only_the_residents_of_the_condominium_of_the_token()
    {
        ManagerSession first = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession second = await ManagerSetup.RegisterAsync(fixture, _client);
        await RegisterAsync(first, await CreateUnitAsync(first, "101"), "Ana Do Primeiro");
        await RegisterAsync(second, await CreateUnitAsync(second, "101"), "Beto Do Segundo");

        Names(await ListAsync(first)).Should().Equal("Ana Do Primeiro");
        Names(await ListAsync(second)).Should().Equal("Beto Do Segundo");
    }

    [Fact]
    public async Task List_of_a_new_condominium_is_empty_and_does_not_include_the_manager()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        JsonElement json = await ListAsync(manager);

        json.GetProperty("total").GetInt32().Should().Be(0);
        json.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    // ---- Apoio ----

    private static JsonObject ResidentBody(Guid unitId, string? cpf = null, string? email = "maria@exemplo.com", string kind = "owner") => new()
    {
        ["fullName"] = "Maria Souza",
        ["cpf"] = cpf ?? CondominiumBuilders.UniqueCpf().Value,
        ["birthDate"] = "1990-05-20",
        ["phone"] = "(47) 99876-5432",
        ["email"] = email,
        ["profession"] = "Engenheira",
        ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
        ["unitId"] = unitId,
        ["kind"] = kind,
        ["startDate"] = "2024-03-01",
    };

    private async Task<JsonElement> CreateUnitJsonAsync(ManagerSession manager, string number, string? block = null)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, new JsonObject { ["block"] = block, ["number"] = number, ["type"] = "apartment" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> CreateUnitAsync(ManagerSession manager, string number, string? block = null) =>
        (await CreateUnitJsonAsync(manager, number, block)).GetProperty("id").GetGuid();

    private async Task<Guid> RegisterAsync(ManagerSession manager, Guid unitId, string name)
    {
        JsonObject body = ResidentBody(unitId);
        body["fullName"] = name;
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ListAsync(ManagerSession manager, string query = "")
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}{query}", manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static IEnumerable<string?> Names(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("fullName").GetString());
}
