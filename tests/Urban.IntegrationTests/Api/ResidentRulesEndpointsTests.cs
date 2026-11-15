using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Infrastructure;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// As regras que o cadastro de moradores ganhou depois dos primeiros testes com a síndica: e-mail único no condomínio, entrada
/// nunca antes do nascimento e o aviso de cadastro incompleto de quem já é adulto.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ResidentRulesEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Residents = "/api/residents";
    private const string Units = "/api/units";
    private const string EmailTaken = "condominium.resident.email_already_registered";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    // ---- E-mail único por condomínio ----

    [Fact]
    public async Task Post_with_an_email_another_resident_already_has_answers_409_on_the_email_even_with_other_letter_case()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        await RegisterAsync(manager, unit, "Ana Lima", email: "ana@exemplo.com");

        HttpResponseMessage response = await PostAsync(manager, Body(unit, "Beto Rocha", email: "  ANA@Exemplo.com "));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be(EmailTaken);
    }

    [Fact]
    public async Task Post_refuses_the_same_email_for_a_dependent_too()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        await RegisterAsync(manager, unit, "Ana Lima", email: "ana@exemplo.com");
        JsonObject dependent = Body(unit, "Pedro Lima", email: "ana@exemplo.com");
        dependent["birthDate"] = "2015-04-10";
        dependent["kind"] = "dependent";
        dependent["profession"] = null;
        dependent["emergencyContact"] = null;

        HttpResponseMessage response = await PostAsync(manager, dependent);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be(EmailTaken);
    }

    [Fact]
    public async Task Post_accepts_the_same_email_in_another_condominium_and_many_residents_without_email()
    {
        (ManagerSession first, Guid firstUnit) = await SeedAsync();
        (ManagerSession second, Guid secondUnit) = await SeedAsync();

        (await PostAsync(first, Body(firstUnit, "Ana Lima", email: "ana@exemplo.com"))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostAsync(second, Body(secondUnit, "Ana Lima", email: "ana@exemplo.com"))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostAsync(first, Body(firstUnit, "Beto Rocha", email: null))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostAsync(first, Body(firstUnit, "Carla Dias", email: null))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Put_with_an_email_another_resident_already_has_answers_409_and_keeping_the_own_email_is_fine()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        await RegisterAsync(manager, unit, "Ana Lima", email: "ana@exemplo.com");
        JsonElement beto = await RegisterAsync(manager, unit, "Beto Rocha", email: "beto@exemplo.com");

        HttpResponseMessage taken = await PutAsync(manager, beto, email: "ANA@exemplo.com");
        HttpResponseMessage same = await PutAsync(manager, beto, email: "beto@exemplo.com");

        taken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await taken.CodeAsync()).Should().Be(EmailTaken);
        same.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_cleared_email_frees_the_address_for_another_resident()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        JsonElement ana = await RegisterAsync(manager, unit, "Ana Lima", email: "ana@exemplo.com");

        (await PutAsync(manager, ana, email: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await PostAsync(manager, Body(unit, "Beto Rocha", email: "ana@exemplo.com"))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---- Entrada nunca antes do nascimento ----

    [Fact]
    public async Task Post_with_an_entry_date_before_the_birth_date_answers_400_on_the_date()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        JsonObject body = Body(unit, "Ana Lima");
        body["birthDate"] = "2000-05-01";
        body["startDate"] = "1999-03-10";

        HttpResponseMessage response = await PostAsync(manager, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.occupancy.start_before_birth");
    }

    [Fact]
    public async Task Post_with_the_entry_on_the_birth_day_is_fine()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        JsonObject body = Body(unit, "Ana Lima");
        body["birthDate"] = "2000-05-01";
        body["startDate"] = "2000-05-01";

        (await PostAsync(manager, body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Adding_a_unit_with_an_entry_before_the_birth_date_answers_400()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        Guid other = await CreateUnitAsync(manager, "102");
        JsonElement ana = await RegisterAsync(manager, unit, "Ana Lima");

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post,
            $"{Residents}/{Id(ana)}/occupancies",
            manager.AccessToken,
            new JsonObject { ["unitId"] = other, ["kind"] = "tenant", ["startDate"] = "1990-05-19" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.occupancy.start_before_birth");
    }

    [Fact]
    public async Task Moving_the_birth_date_to_after_an_entry_answers_400_and_an_earlier_one_is_fine()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        JsonElement ana = await RegisterAsync(manager, unit, "Ana Lima"); // entrou em 01/03/2024

        HttpResponseMessage later = await PutAsync(manager, ana, birthDate: "2024-03-02");
        HttpResponseMessage earlier = await PutAsync(manager, ana, birthDate: "1985-01-01");

        later.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await later.CodeAsync()).Should().Be("condominium.resident.birth_after_occupancy");
        earlier.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Cadastro incompleto ----

    [Fact]
    public async Task A_complete_adult_and_a_minor_have_no_missing_data()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        JsonElement ana = await RegisterAsync(manager, unit, "Ana Lima");
        JsonObject minor = Body(unit, "Pedro Lima", email: null);
        minor["birthDate"] = "2015-04-10";
        minor["kind"] = "dependent";
        minor["profession"] = null;
        minor["emergencyContact"] = null;
        HttpResponseMessage created = await PostAsync(manager, minor);

        ana.GetProperty("missingData").GetArrayLength().Should().Be(0);
        (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("missingData").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_incomplete_filter_lists_only_adults_missing_profession_or_contact_and_the_view_names_what_is_missing()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        await RegisterAsync(manager, unit, "Ana Completa");
        JsonElement beto = await RegisterAsync(manager, unit, "Beto Sem Profissao");
        JsonElement carla = await RegisterAsync(manager, unit, "Carla Sem Nada");
        JsonObject minor = Body(unit, "Davi Menor", email: null);
        minor["birthDate"] = "2015-04-10";
        minor["kind"] = "dependent";
        minor["profession"] = null;
        minor["emergencyContact"] = null;
        (await PostAsync(manager, minor)).StatusCode.Should().Be(HttpStatusCode.Created);
        await ClearAsync(Id(beto), profession: true, contact: false);
        await ClearAsync(Id(carla), profession: true, contact: true);

        JsonElement all = await ListAsync(manager, "");
        JsonElement incomplete = await ListAsync(manager, "?registration=incomplete");

        Names(all).Should().Equal("Ana Completa", "Beto Sem Profissao", "Carla Sem Nada", "Davi Menor");
        Names(incomplete).Should().Equal("Beto Sem Profissao", "Carla Sem Nada");
        incomplete.GetProperty("total").GetInt32().Should().Be(2);
        MissingOf(incomplete, "Beto Sem Profissao").Should().Equal("profession");
        MissingOf(incomplete, "Carla Sem Nada").Should().Equal("profession", "emergencyContact");
        Names(await ListAsync(manager, "?registration=ALL")).Should().HaveCount(4);
        Names(await ListAsync(manager, "?registration=")).Should().HaveCount(4);
    }

    [Fact]
    public async Task The_incomplete_filter_combines_with_the_status_and_the_search()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        JsonElement beto = await RegisterAsync(manager, unit, "Beto Sem Profissao");
        JsonElement bia = await RegisterAsync(manager, unit, "Bia Sem Profissao");
        await ClearAsync(Id(beto), profession: true, contact: false);
        await ClearAsync(Id(bia), profession: true, contact: false);

        Names(await ListAsync(manager, "?registration=incomplete&search=bia")).Should().Equal("Bia Sem Profissao");
        Names(await ListAsync(manager, "?registration=incomplete&status=former")).Should().BeEmpty();
        Names(await ListAsync(manager, "?registration=incomplete&status=active")).Should().Equal("Beto Sem Profissao", "Bia Sem Profissao");
    }

    [Theory]
    [InlineData("?registration=complete")]
    [InlineData("?registration=1")]
    public async Task List_with_an_unknown_registration_answers_400(string query)
    {
        (ManagerSession manager, _) = await SeedAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}{query}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("request.invalid");
    }

    [Fact]
    public async Task Editing_a_resident_that_is_missing_data_asks_for_it_and_saving_it_clears_the_warning()
    {
        (ManagerSession manager, Guid unit) = await SeedAsync();
        JsonElement beto = await RegisterAsync(manager, unit, "Beto Sem Profissao");
        await ClearAsync(Id(beto), profession: true, contact: false);
        JsonElement stale = await ReadAsync(manager, Id(beto));

        HttpResponseMessage withoutProfession = await PutAsync(manager, stale, profession: null);
        HttpResponseMessage filled = await PutAsync(manager, stale, profession: "Professor");

        withoutProfession.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await withoutProfession.CodeAsync()).Should().Be("condominium.profession.invalid");
        filled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await filled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("missingData").GetArrayLength().Should().Be(0);
        Names(await ListAsync(manager, "?registration=incomplete")).Should().BeEmpty();
    }

    // ---- Apoio ----

    private async Task<(ManagerSession Manager, Guid UnitId)> SeedAsync()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        return (manager, await CreateUnitAsync(manager, "101"));
    }

    private async Task<Guid> CreateUnitAsync(ManagerSession manager, string number)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, new JsonObject { ["number"] = number, ["type"] = "apartment" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static JsonObject Body(Guid unitId, string name, string? email = "") => new()
    {
        ["fullName"] = name,
        ["cpf"] = CondominiumBuilders.UniqueCpf().Value,
        ["birthDate"] = "1990-05-20",
        ["phone"] = "(47) 99876-5432",
        ["email"] = email == string.Empty ? $"{Guid.NewGuid():N}@exemplo.com" : email,
        ["profession"] = "Engenheira",
        ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
        ["unitId"] = unitId,
        ["kind"] = "owner",
        ["startDate"] = "2024-03-01",
    };

    private Task<HttpResponseMessage> PostAsync(ManagerSession manager, JsonObject body) =>
        _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);

    private async Task<JsonElement> RegisterAsync(ManagerSession manager, Guid unitId, string name, string? email = "")
    {
        HttpResponseMessage response = await PostAsync(manager, Body(unitId, name, email));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Edita repetindo os dados lidos; cada teste muda só o campo que quer provar.</summary>
    private Task<HttpResponseMessage> PutAsync(ManagerSession manager, JsonElement resident, string? email = "keep", string? birthDate = null, string? profession = "keep")
    {
        JsonObject body = new()
        {
            ["version"] = resident.GetProperty("version").GetString(),
            ["fullName"] = resident.GetProperty("fullName").GetString(),
            ["cpf"] = null,
            ["birthDate"] = birthDate ?? resident.GetProperty("birthDate").GetString(),
            ["phone"] = resident.GetProperty("phone").GetString(),
            ["email"] = email == "keep" ? resident.GetProperty("email").GetString() : email,
            ["profession"] = profession == "keep" ? resident.GetProperty("profession").GetString() : profession,
            ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
        };

        return _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{Id(resident)}", manager.AccessToken, body);
    }

    private async Task<JsonElement> ReadAsync(ManagerSession manager, Guid id)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}/{id}", manager.AccessToken);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ListAsync(ManagerSession manager, string query)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}{query}", manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Deixa o adulto como os cadastros antigos (ou o dependente que fez 18 anos) ficam: sem profissão e/ou sem contato. A API não deixa criar assim.</summary>
    private async Task ClearAsync(Guid residentId, bool profession, bool contact)
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        CondominiumDbContext db = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();
        if (profession)
        {
            await db.Database.ExecuteSqlAsync($"update condominium.person set profession = null where id = {residentId}", Ct);
        }

        if (contact)
        {
            await db.Database.ExecuteSqlAsync(
                $"update condominium.person set emergency_contact_name = null, emergency_contact_relationship = null, emergency_contact_phone = null where id = {residentId}",
                Ct);
        }
    }

    private static Guid Id(JsonElement resident) => resident.GetProperty("id").GetGuid();

    private static IEnumerable<string?> Names(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("fullName").GetString());

    private static IEnumerable<string?> MissingOf(JsonElement list, string name) =>
        list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("fullName").GetString() == name)
            .GetProperty("missingData").EnumerateArray().Select(i => i.GetString());
}
