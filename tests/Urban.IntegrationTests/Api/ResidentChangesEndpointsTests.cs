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

/// <summary>Editar o morador, abrir um vínculo novo e sair da unidade, de ponta a ponta.</summary>
[Collection(ApiCollection.Name)]
public sealed class ResidentChangesEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Residents = "/api/residents";
    private const string Units = "/api/units";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    // ---- Acesso ----

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "PUT", $"{Residents}/{Guid.Empty}" },
        { "POST", $"{Residents}/{Guid.Empty}/occupancies" },
        { "POST", $"{Residents}/{Guid.Empty}/occupancies/{Guid.Empty}/end" },
    };

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Every_route_requires_a_token(string method, string route)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, null, new JsonObject());

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

        foreach ((string method, string route) in new[] { ("PUT", $"{Residents}/{Guid.NewGuid()}"), ("POST", $"{Residents}/{Guid.NewGuid()}/occupancies"), ("POST", $"{Residents}/{Guid.NewGuid()}/occupancies/{Guid.NewGuid()}/end") })
        {
            HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, token, new JsonObject());

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

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Guid.NewGuid()}", token, EditBody("4"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.not_found");
    }

    // ---- Editar ----

    [Fact]
    public async Task Put_updates_the_resident_and_answers_200_with_the_new_version()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        JsonObject body = EditBody(resident.GetProperty("version").GetString()!);
        body["phone"] = "(47) 99111-2222";
        body["profession"] = "Médica";
        body["email"] = null;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("phone").GetString().Should().Be("47991112222");
        json.GetProperty("profession").GetString().Should().Be("Médica");
        json.GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("version").GetString().Should().NotBe(resident.GetProperty("version").GetString());
        json.GetProperty("occupancies").GetArrayLength().Should().Be(1);

        JsonElement reread = await GetAsync(manager, Id(resident));
        reread.GetProperty("version").GetString().Should().Be(json.GetProperty("version").GetString());
        reread.GetProperty("profession").GetString().Should().Be("Médica");
    }

    [Fact]
    public async Task Put_with_nothing_changed_answers_200_and_keeps_the_version()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, EditBody(resident.GetProperty("version").GetString()!));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString().Should().Be(resident.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Put_with_a_stale_version_answers_409_and_changes_nothing()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        string version = resident.GetProperty("version").GetString()!;
        JsonObject first = EditBody(version);
        first["phone"] = "(47) 99111-2222";
        (await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, first)).StatusCode.Should().Be(HttpStatusCode.OK);
        JsonObject stale = EditBody(version);
        stale["phone"] = "(47) 97777-6666";

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, stale);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("concurrency.conflict");
        (await GetAsync(manager, Id(resident))).GetProperty("phone").GetString().Should().Be("47991112222");
    }

    [Fact]
    public async Task Put_without_the_version_answers_400()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        JsonObject body = EditBody("4");
        body["version"] = null;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.version.required");
    }

    [Theory]
    [InlineData("fullName", "Maria", "condominium.person.full_name_invalid")]
    [InlineData("cpf", "111.111.111-11", "condominium.cpf.invalid")]
    [InlineData("birthDate", null, "condominium.birth_date.invalid")]
    [InlineData("birthDate", "UNDER_AGE", "condominium.resident.under_age")]
    [InlineData("phone", "123", "condominium.phone.invalid")]
    [InlineData("email", "sem-arroba", "condominium.email.invalid")]
    [InlineData("profession", "x", "condominium.profession.invalid")]
    public async Task Put_with_an_invalid_field_answers_400_with_the_code_of_the_field(string field, string? value, string code)
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        JsonObject body = EditBody(resident.GetProperty("version").GetString()!);
        body[field] = value == "UNDER_AGE" ? DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd") : value;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Put_with_the_cpf_of_another_resident_answers_409()
    {
        (ManagerSession manager, JsonElement resident, Guid unitId) = await SeedAsync();
        string takenCpf = CondominiumBuilders.UniqueCpf().Value;
        await RegisterAsync(manager, unitId, "Outra Pessoa", takenCpf);
        JsonObject body = EditBody(resident.GetProperty("version").GetString()!, cpf: takenCpf);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be(PersonErrors.CpfAlreadyRegistered.Code);
    }

    [Fact]
    public async Task Put_of_a_resident_of_another_condominium_answers_404()
    {
        (ManagerSession mine, _, _) = await SeedAsync();
        (_, JsonElement foreign, _) = await SeedAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(foreign)}", mine.AccessToken, EditBody("4"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.resident.not_found");
    }

    // ---- Novo vínculo ----

    [Fact]
    public async Task Put_edits_a_minor_dependent_without_profession_nor_emergency_contact()
    {
        (ManagerSession manager, _, Guid unitId) = await SeedAsync();
        JsonElement minor = await RegisterMinorAsync(manager, unitId);
        JsonObject body = MinorEditBody(minor.GetProperty("version").GetString()!);
        body["phone"] = "(47) 99111-2222";

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(minor)}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("phone").GetString().Should().Be("47991112222");
        json.GetProperty("profession").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("emergencyContact").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_that_turns_a_minor_into_an_adult_requires_profession_and_emergency_contact()
    {
        (ManagerSession manager, _, Guid unitId) = await SeedAsync();
        JsonElement minor = await RegisterMinorAsync(manager, unitId);
        JsonObject body = MinorEditBody(minor.GetProperty("version").GetString()!);
        body["birthDate"] = "1990-05-20";

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(minor)}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.profession.invalid");
    }

    [Fact]
    public async Task Put_that_makes_an_owner_a_minor_answers_400_under_age()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        JsonObject body = EditBody(resident.GetProperty("version").GetString()!);
        body["birthDate"] = "2015-04-10";
        body["profession"] = null;
        body["emergencyContact"] = null;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.resident.under_age");
    }

    [Fact]
    public async Task Post_occupancy_of_a_minor_accepts_only_the_dependent_kind()
    {
        (ManagerSession manager, _, Guid unitId) = await SeedAsync();
        JsonElement minor = await RegisterMinorAsync(manager, unitId);
        Guid other = await CreateUnitAsync(manager, "202", "Bloco B");

        HttpResponseMessage tenant = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(minor)}/occupancies", manager.AccessToken, OccupancyBody(other, "tenant", "2025-06-01"));
        HttpResponseMessage dependent = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(minor)}/occupancies", manager.AccessToken, OccupancyBody(other, "dependent", "2025-06-01"));

        tenant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tenant.CodeAsync()).Should().Be("condominium.resident.under_age");
        dependent.StatusCode.Should().Be(HttpStatusCode.Created, await dependent.Content.ReadAsStringAsync(Ct));
        JsonElement json = await dependent.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("occupancies").EnumerateArray().Should().HaveCount(2).And.OnlyContain(o => o.GetProperty("kind").GetString() == "dependent");
    }

    [Fact]
    public async Task Post_occupancy_with_the_dependent_kind_is_valid_for_an_adult()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        Guid other = await CreateUnitAsync(manager, "202", "Bloco B");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(other, "dependent", "2025-06-01"));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Post_occupancy_adds_a_second_unit_and_answers_201_with_the_resident()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        Guid other = await CreateUnitAsync(manager, "202", "Bloco B");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(other, "tenant", "2025-06-01"));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.Location!.ToString().Should().EndWith($"{Residents}/{Id(resident)}");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement[] occupancies = [.. json.GetProperty("occupancies").EnumerateArray()];
        occupancies.Should().HaveCount(2);
        occupancies.Should().ContainSingle(o => o.GetProperty("unitId").GetGuid() == other)
            .Which.GetProperty("kind").GetString().Should().Be("tenant");
    }

    [Fact]
    public async Task Post_occupancy_in_the_unit_where_the_resident_already_lives_answers_409()
    {
        (ManagerSession manager, JsonElement resident, Guid unitId) = await SeedAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(unitId, "tenant", "2025-06-01"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("condominium.occupancy.already_active");
    }

    [Theory]
    [InlineData("castelo", "2025-06-01", "condominium.occupancy.kind_invalid")]
    [InlineData(null, "2025-06-01", "condominium.occupancy.kind_invalid")]
    [InlineData("owner", null, "condominium.occupancy.start_date_invalid")]
    [InlineData("owner", "1800-01-01", "condominium.occupancy.start_date_invalid")]
    public async Task Post_occupancy_with_an_invalid_field_answers_400_with_the_code_of_the_field(string? kind, string? start, string code)
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        Guid other = await CreateUnitAsync(manager, "202");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(other, kind, start));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Post_occupancy_with_an_unknown_or_foreign_unit_answers_404_and_with_a_deactivated_one_409()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        (ManagerSession otherManager, _, Guid foreignUnit) = await SeedAsync();
        JsonElement deactivated = await CreateUnitJsonAsync(manager, "303");
        await _client.SendJsonAsync(HttpMethod.Post, $"{Units}/{deactivated.GetProperty("id").GetGuid()}/deactivate", manager.AccessToken, new JsonObject { ["version"] = deactivated.GetProperty("version").GetString() });

        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(Guid.NewGuid(), "owner", "2025-06-01"));
        HttpResponseMessage foreign = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(foreignUnit, "owner", "2025-06-01"));
        HttpResponseMessage off = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(deactivated.GetProperty("id").GetGuid(), "owner", "2025-06-01"));

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.CodeAsync()).Should().Be(UnitErrors.NotFound.Code);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        off.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await off.CodeAsync()).Should().Be(UnitErrors.Deactivated.Code);
        otherManager.CondominiumId.Should().NotBe(manager.CondominiumId);
    }

    [Fact]
    public async Task Post_occupancy_of_an_unknown_resident_answers_404()
    {
        (ManagerSession manager, _, Guid unitId) = await SeedAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Guid.NewGuid()}/occupancies", manager.AccessToken, OccupancyBody(unitId, "owner", "2025-06-01"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.resident.not_found");
    }

    // ---- Sair da unidade ----

    [Fact]
    public async Task Post_end_fills_the_end_date_and_keeps_the_history()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        Guid occupancyId = FirstOccupancyId(resident);
        string end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies/{occupancyId}/end", manager.AccessToken, new JsonObject { ["endDate"] = end });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("occupancies").EnumerateArray().Single().GetProperty("endDate").GetString().Should().Be(end);
        (await GetAsync(manager, Id(resident))).GetProperty("occupancies").EnumerateArray().Single().GetProperty("endDate").GetString().Should().Be(end);
    }

    [Fact]
    public async Task Post_end_twice_answers_409()
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        string route = $"{Residents}/{Id(resident)}/occupancies/{FirstOccupancyId(resident)}/end";
        JsonObject body = new() { ["endDate"] = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd") };
        (await _client.SendJsonAsync(HttpMethod.Post, route, manager.AccessToken, body)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage again = await _client.SendJsonAsync(HttpMethod.Post, route, manager.AccessToken, body);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.CodeAsync()).Should().Be("condominium.occupancy.already_ended");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2000-01-01")]
    [InlineData("TOMORROW")]
    public async Task Post_end_with_an_invalid_date_answers_400(string? end)
    {
        (ManagerSession manager, JsonElement resident, _) = await SeedAsync();
        JsonObject body = new() { ["endDate"] = end == "TOMORROW" ? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd") : end };

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies/{FirstOccupancyId(resident)}/end", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.occupancy.end_date_invalid");
    }

    [Fact]
    public async Task Post_end_of_an_occupancy_that_is_not_of_the_resident_answers_404()
    {
        (ManagerSession manager, JsonElement resident, Guid unitId) = await SeedAsync();
        JsonElement other = await RegisterAsync(manager, unitId, "Outra Pessoa", CondominiumBuilders.UniqueCpf().Value);
        JsonObject body = new() { ["endDate"] = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd") };

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies/{FirstOccupancyId(other)}/end", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.occupancy.not_found");
    }

    [Fact]
    public async Task After_leaving_the_resident_can_move_back_to_the_same_unit()
    {
        (ManagerSession manager, JsonElement resident, Guid unitId) = await SeedAsync();
        JsonObject end = new() { ["endDate"] = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd") };
        await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies/{FirstOccupancyId(resident)}/end", manager.AccessToken, end);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{Id(resident)}/occupancies", manager.AccessToken, OccupancyBody(unitId, "tenant", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("occupancies").GetArrayLength().Should().Be(2);
    }

    // ---- Apoio ----

    private static Guid Id(JsonElement resident) => resident.GetProperty("id").GetGuid();

    private static Guid FirstOccupancyId(JsonElement resident) => resident.GetProperty("occupancies").EnumerateArray().First().GetProperty("id").GetGuid();

    private static JsonObject EditBody(string version, string? cpf = null) => new()
    {
        ["version"] = version,
        ["fullName"] = "Maria Souza",
        ["cpf"] = cpf,
        ["birthDate"] = "1990-05-20",
        ["phone"] = "(47) 99876-5432",
        ["email"] = "maria@exemplo.com",
        ["profession"] = "Engenheira",
        ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
    };

    private static JsonObject OccupancyBody(Guid unitId, string? kind, string? startDate) => new()
    {
        ["unitId"] = unitId,
        ["kind"] = kind,
        ["startDate"] = startDate,
    };

    private async Task<(ManagerSession Manager, JsonElement Resident, Guid UnitId)> SeedAsync()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await CreateUnitAsync(manager, "101", "Bloco A");
        JsonElement resident = await RegisterAsync(manager, unitId, "Maria Souza", CondominiumBuilders.UniqueCpf().Value);

        return (manager, resident, unitId);
    }

    private async Task<JsonElement> RegisterAsync(ManagerSession manager, Guid unitId, string name, string cpf)
    {
        JsonObject body = new()
        {
            ["fullName"] = name,
            ["cpf"] = cpf,
            ["birthDate"] = "1990-05-20",
            ["phone"] = "(47) 99876-5432",
            ["email"] = "maria@exemplo.com",
            ["profession"] = "Engenheira",
            ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
            ["unitId"] = unitId,
            ["kind"] = "owner",
            ["startDate"] = "2024-03-01",
        };
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonObject MinorEditBody(string version) => new()
    {
        ["version"] = version,
        ["fullName"] = "Pedro Souza",
        ["cpf"] = null,
        ["birthDate"] = "2015-04-10",
        ["phone"] = "(47) 99876-5432",
        ["email"] = null,
        ["profession"] = null,
        ["emergencyContact"] = null,
    };

    private async Task<JsonElement> RegisterMinorAsync(ManagerSession manager, Guid unitId)
    {
        JsonObject body = new()
        {
            ["fullName"] = "Pedro Souza",
            ["cpf"] = CondominiumBuilders.UniqueCpf().Value,
            ["birthDate"] = "2015-04-10",
            ["phone"] = "(47) 99876-5432",
            ["unitId"] = unitId,
            ["kind"] = "dependent",
            ["startDate"] = "2024-03-01",
        };
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> GetAsync(ManagerSession manager, Guid id)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}/{id}", manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> CreateUnitJsonAsync(ManagerSession manager, string number, string? block = null)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, new JsonObject { ["block"] = block, ["number"] = number, ["type"] = "apartment" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> CreateUnitAsync(ManagerSession manager, string number, string? block = null) =>
        (await CreateUnitJsonAsync(manager, number, block)).GetProperty("id").GetGuid();
}
