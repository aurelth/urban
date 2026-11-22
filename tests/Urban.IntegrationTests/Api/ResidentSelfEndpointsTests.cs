using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// O que cada conta vê de si mesma: "Meus dados" do morador, a unidade do síndico que mora no condomínio (pelas rotas de moradores,
/// com o id que a leitura devolve) e o nome no cabeçalho de quem ainda não escolheu como prefere ser chamado.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ResidentSelfEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Residents = "/api/residents";
    private const string MineRoute = Residents + "/me";
    private const string AccessRoute = "/api/finance/me/access";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    // ---- Acesso ----

    [Fact]
    public async Task The_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MineRoute, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_super_admin_has_no_registration_and_is_refused()
    {
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: UserRole.SuperAdmin);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MineRoute, await _client.SignInAsync(user));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_condominium_member");
    }

    // ---- Meus dados do morador ----

    [Fact]
    public async Task A_resident_reads_his_own_registration_with_the_unit_he_lives_in()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await ResidentSetup.CreateUnitAsync(_client, manager, "101");
        string token = await ResidentSetup.TokenAsync(fixture, _client, manager, unitId);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MineRoute, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("fullName").GetString().Should().Be("Maria Souza");
        body.GetProperty("maskedCpf").GetString().Should().MatchRegex(@"^\*\*\*\.\d{3}\.\d{3}-\*\*$");
        body.GetProperty("birthDate").GetString().Should().Be("1990-05-20");
        body.GetProperty("phone").GetString().Should().Be("47998765432");
        body.GetProperty("email").GetString().Should().StartWith("morador-");
        body.GetProperty("profession").GetString().Should().Be("Engenheira");
        body.GetProperty("emergencyContact").GetProperty("name").GetString().Should().Be("Carlos Souza");
        body.GetProperty("accessStatus").GetString().Should().Be("active");
        JsonElement unit = body.GetProperty("occupancies").EnumerateArray().Should().ContainSingle().Subject;
        unit.GetProperty("unitNumber").GetString().Should().Be("101");
        unit.GetProperty("kind").GetString().Should().Be("owner");
        unit.GetProperty("startDate").GetString().Should().Be("2024-03-01");
        unit.GetProperty("endDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Each_resident_reads_only_his_own_registration()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid first = await ResidentSetup.CreateUnitAsync(_client, manager, "101");
        Guid second = await ResidentSetup.CreateUnitAsync(_client, manager, "102");
        string maria = await ResidentSetup.TokenAsync(fixture, _client, manager, first, fullName: "Maria Souza");
        string paulo = await ResidentSetup.TokenAsync(fixture, _client, manager, second, fullName: "Paulo Lima");

        JsonElement mine = await (await _client.SendJsonAsync(HttpMethod.Get, MineRoute, maria)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        JsonElement his = await (await _client.SendJsonAsync(HttpMethod.Get, MineRoute, paulo)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        mine.GetProperty("fullName").GetString().Should().Be("Maria Souza");
        mine.GetProperty("occupancies").EnumerateArray().Single().GetProperty("unitNumber").GetString().Should().Be("101");
        his.GetProperty("fullName").GetString().Should().Be("Paulo Lima");
        his.GetProperty("occupancies").EnumerateArray().Single().GetProperty("unitNumber").GetString().Should().Be("102");
    }

    [Fact]
    public async Task A_resident_cannot_give_himself_a_unit()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid first = await ResidentSetup.CreateUnitAsync(_client, manager, "101");
        Guid other = await ResidentSetup.CreateUnitAsync(_client, manager, "102");
        string token = await ResidentSetup.TokenAsync(fixture, _client, manager, first);
        Guid myId = (await (await _client.SendJsonAsync(HttpMethod.Get, MineRoute, token)).Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post, $"{Residents}/{myId}/occupancies", token, new JsonObject { ["unitId"] = other, ["kind"] = "owner", ["startDate"] = "2024-03-01" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    // ---- A unidade do síndico ----

    [Fact]
    public async Task A_manager_without_a_unit_reads_his_own_person_with_no_occupancies()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MineRoute, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.GetProperty("fullName").GetString().Should().Be("Ana Maria Silva");
        body.GetProperty("occupancies").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_manager_registers_the_unit_he_lives_in_and_then_moves_out_of_it()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await ResidentSetup.CreateUnitAsync(_client, manager, "101");
        Guid myId = await MyPersonIdAsync(manager);

        HttpResponseMessage added = await _client.SendJsonAsync(
            HttpMethod.Post, $"{Residents}/{myId}/occupancies", manager.AccessToken, new JsonObject { ["unitId"] = unitId, ["kind"] = "owner", ["startDate"] = "2024-03-01" });

        added.StatusCode.Should().Be(HttpStatusCode.Created, await added.Content.ReadAsStringAsync(Ct));
        JsonElement mine = await (await _client.SendJsonAsync(HttpMethod.Get, MineRoute, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        JsonElement occupancy = mine.GetProperty("occupancies").EnumerateArray().Should().ContainSingle().Subject;
        occupancy.GetProperty("unitNumber").GetString().Should().Be("101");
        (await AccessOfAsync(manager.AccessToken)).Should().Be("unit");

        HttpResponseMessage ended = await _client.SendJsonAsync(
            HttpMethod.Post,
            $"{Residents}/{myId}/occupancies/{occupancy.GetProperty("id").GetGuid()}/end",
            manager.AccessToken,
            new JsonObject { ["endDate"] = "2024-06-30" });

        ended.StatusCode.Should().Be(HttpStatusCode.OK, await ended.Content.ReadAsStringAsync(Ct));
        (await AccessOfAsync(manager.AccessToken)).Should().Be("none");
    }

    [Fact]
    public async Task The_manager_who_lives_in_a_unit_does_not_show_in_the_residents_list()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await ResidentSetup.CreateUnitAsync(_client, manager, "101");
        Guid other = await ResidentSetup.CreateUnitAsync(_client, manager, "102");
        Guid myId = await MyPersonIdAsync(manager);
        (await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{myId}/occupancies", manager.AccessToken, new JsonObject { ["unitId"] = unitId, ["kind"] = "owner", ["startDate"] = "2024-03-01" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        await ResidentSetup.TokenAsync(fixture, _client, manager, other);

        JsonElement list = await (await _client.SendJsonAsync(HttpMethod.Get, Residents, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        list.GetProperty("total").GetInt32().Should().Be(1);
        list.GetProperty("items").EnumerateArray().Single().GetProperty("fullName").GetString().Should().Be("Maria Souza");
    }

    [Fact]
    public async Task The_manager_cannot_edit_or_invite_his_own_person_as_if_he_were_a_resident()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await ResidentSetup.CreateUnitAsync(_client, manager, "101");
        Guid myId = await MyPersonIdAsync(manager);
        await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{myId}/occupancies", manager.AccessToken, new JsonObject { ["unitId"] = unitId, ["kind"] = "owner", ["startDate"] = "2024-03-01" });
        JsonObject edit = new()
        {
            ["version"] = "1",
            ["fullName"] = "Ana Maria Silva",
            ["phone"] = "(47) 99123-4567",
            ["birthDate"] = "1985-03-12",
            ["profession"] = "Síndica",
            ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
        };

        HttpResponseMessage update = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{myId}", manager.AccessToken, edit);
        HttpResponseMessage invite = await _client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{myId}/invitation", manager.AccessToken);

        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await update.CodeAsync()).Should().Be("condominium.resident.not_found");
        invite.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await invite.CodeAsync()).Should().Be("condominium.resident.not_found");
    }

    // ---- O nome no cabeçalho ----

    [Fact]
    public async Task A_resident_who_never_chose_a_name_gets_the_first_name_of_his_registration_in_me()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid unitId = await ResidentSetup.CreateUnitAsync(_client, manager, "101");
        string token = await ResidentSetup.TokenAsync(fixture, _client, manager, unitId, fullName: "Maria Souza Lima");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, "/api/auth/me", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("role").GetString().Should().Be("Resident");
        body.GetProperty("displayName").GetString().Should().Be("Maria");
    }

    [Fact]
    public async Task The_manager_keeps_the_name_he_chose_in_me()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        JsonElement body = await (await _client.SendJsonAsync(HttpMethod.Get, "/api/auth/me", manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        body.GetProperty("displayName").GetString().Should().Be("Ana");
    }

    // ---- Apoio ----

    private async Task<Guid> MyPersonIdAsync(ManagerSession manager)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MineRoute, manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<string?> AccessOfAsync(string token)
    {
        JsonElement body = await (await _client.SendJsonAsync(HttpMethod.Get, AccessRoute, token)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        return body.GetProperty("access").GetString();
    }
}
