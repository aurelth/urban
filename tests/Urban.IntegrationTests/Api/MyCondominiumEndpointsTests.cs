using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class MyCondominiumEndpointsTests(ApiFixture fixture) : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "phone", "123", "condominium.phone.invalid" },
        { "phone", "(47) 3333-4444", "condominium.phone.mobile_required" },
        { "managerAddress.postalCode", "12", "condominium.postal_code.invalid" },
        { "managerAddress.state", "XX", "condominium.address.state_invalid" },
        { "managerAddress.street", "", "condominium.address.street_invalid" },
        { "condominiumName", "A", "condominium.name.invalid" },
        { "condominiumAddress.city", " ", "condominium.address.city_invalid" },
        { "displayName", "1", "identity.display_name.invalid" },
    };

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Get_returns_the_data_of_the_manager_with_the_cpf_masked_and_forbids_caching()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerSetup.MineRoute, manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        string raw = await response.Content.ReadAsStringAsync(Ct);
        JsonElement body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("condominiumId").GetGuid().Should().Be(manager.CondominiumId);
        body.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("displayName").GetString().Should().Be("Ana");
        body.GetProperty("manager").GetProperty("fullName").GetString().Should().Be("Ana Maria Silva");
        body.GetProperty("manager").GetProperty("maskedCpf").GetString().Should().Be("***.982.247-**");
        body.GetProperty("manager").GetProperty("birthDate").GetString().Should().Be("1985-03-12");
        body.GetProperty("manager").GetProperty("phone").GetString().Should().Be("47991234567");
        body.GetProperty("manager").GetProperty("address").GetProperty("city").GetString().Should().Be("Blumenau");
        body.GetProperty("condominium").GetProperty("name").GetString().Should().Be("Residencial Vila Verde");
        body.GetProperty("condominium").GetProperty("cnpj").GetString().Should().MatchRegex(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$");
        body.GetProperty("condominium").GetProperty("address").GetProperty("city").GetString().Should().Be("Joinville");
        raw.Should().NotContain(ManagerSetup.Cpf);
    }

    [Fact]
    public async Task Get_without_a_token_is_401()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerSetup.MineRoute, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Only_a_manager_can_read_and_change_the_data(UserRole role)
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: role);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage read = await _client.SendJsonAsync(HttpMethod.Get, ManagerSetup.MineRoute, token);
        HttpResponseMessage write = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, token, ManagerSetup.FreeFieldsBody("1.1"));

        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await read.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    [Fact]
    public async Task A_manager_who_has_not_registered_the_condominium_gets_404()
    {
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage read = await _client.SendJsonAsync(HttpMethod.Get, ManagerSetup.MineRoute, token);
        HttpResponseMessage write = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, token, ManagerSetup.FreeFieldsBody("1.1"));

        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await read.CodeAsync()).Should().Be("condominium.not_found");
        write.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_changes_the_free_fields_returns_the_new_data_and_the_next_get_shows_them()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement before = await _client.ReadMineAsync(manager.AccessToken);
        JsonObject body = ManagerSetup.FreeFieldsBody(before.GetProperty("version").GetString()!, displayName: "Aninha", phone: "(47) 98888-7766", condominiumName: "  Vila   Nova  ");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement saved = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        saved.GetProperty("version").GetString().Should().NotBe(before.GetProperty("version").GetString());
        saved.GetProperty("displayName").GetString().Should().Be("Aninha");
        saved.GetProperty("manager").GetProperty("phone").GetString().Should().Be("47988887766");
        saved.GetProperty("condominium").GetProperty("name").GetString().Should().Be("Vila Nova");
        JsonElement after = await _client.ReadMineAsync(manager.AccessToken);
        after.GetRawText().Should().Be(saved.GetRawText());
        after.GetProperty("condominium").GetProperty("cnpj").GetString().Should().Be(before.GetProperty("condominium").GetProperty("cnpj").GetString());
    }

    [Fact]
    public async Task Put_ignores_the_fields_that_cannot_be_changed_here()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement before = await _client.ReadMineAsync(manager.AccessToken);
        JsonObject body = ManagerSetup.FreeFieldsBody(before.GetProperty("version").GetString()!);
        body["cnpj"] = "11222333000181";
        body["cpf"] = "11144477735";
        body["fullName"] = "Outra Pessoa";
        body["birthDate"] = "1990-01-01";

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement after = await _client.ReadMineAsync(manager.AccessToken);
        after.GetProperty("condominium").GetProperty("cnpj").GetString().Should().Be(before.GetProperty("condominium").GetProperty("cnpj").GetString());
        after.GetProperty("manager").GetProperty("maskedCpf").GetString().Should().Be("***.982.247-**");
        after.GetProperty("manager").GetProperty("fullName").GetString().Should().Be("Ana Maria Silva");
        after.GetProperty("manager").GetProperty("birthDate").GetString().Should().Be("1985-03-12");
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Put_with_an_invalid_field_is_400_with_the_code_and_saves_nothing(string path, string value, string code)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement before = await _client.ReadMineAsync(manager.AccessToken);
        JsonObject body = With(ManagerSetup.FreeFieldsBody(before.GetProperty("version").GetString()!), path, value);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
        (await _client.ReadMineAsync(manager.AccessToken)).GetRawText().Should().Be(before.GetRawText());
    }

    [Fact]
    public async Task Put_without_the_version_is_400()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject body = ManagerSetup.FreeFieldsBody("x");
        body.Remove("version");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.version.required");
    }

    [Fact]
    public async Task Put_with_a_stale_version_is_409_and_the_other_change_is_kept()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        string staleVersion = (await _client.ReadMineAsync(manager.AccessToken)).GetProperty("version").GetString()!;
        (await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, manager.AccessToken, ManagerSetup.FreeFieldsBody(staleVersion, phone: "47977776666")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, manager.AccessToken, ManagerSetup.FreeFieldsBody(staleVersion, phone: "47955554444"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("concurrency.conflict");
        (await _client.ReadMineAsync(manager.AccessToken)).GetProperty("manager").GetProperty("phone").GetString().Should().Be("47977776666");
    }

    [Fact]
    public async Task Put_without_a_token_is_401()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, null, ManagerSetup.FreeFieldsBody("1.1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Each_manager_only_sees_and_changes_their_own_condominium()
    {
        ManagerSession first = await ManagerSetup.RegisterAsync(fixture, _client);
        ManagerSession second = await ManagerSetup.RegisterAsync(fixture, _client);
        string version = (await _client.ReadMineAsync(second.AccessToken)).GetProperty("version").GetString()!;

        (await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, second.AccessToken, ManagerSetup.FreeFieldsBody(version, condominiumName: "Condomínio do segundo")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.ReadMineAsync(first.AccessToken)).GetProperty("condominium").GetProperty("name").GetString().Should().Be("Residencial Vila Verde");
        (await _client.ReadMineAsync(first.AccessToken)).GetProperty("condominiumId").GetGuid().Should().Be(first.CondominiumId);
    }

    [Fact]
    public async Task Put_writes_one_log_entry_with_the_names_of_the_fields_and_never_their_values()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        string version = (await _client.ReadMineAsync(manager.AccessToken)).GetProperty("version").GetString()!;

        await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.MineRoute, manager.AccessToken, ManagerSetup.FreeFieldsBody(version, phone: "47966665555"));

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        string[] fields = await db.Database
            .SqlQuery<string>($"""select array_to_string(fields, ',') as "Value" from condominium.change_log where condominium_id = {manager.CondominiumId} and kind = 'profile'""")
            .ToArrayAsync(Ct);
        fields.Should().ContainSingle().Which.Should().Be("phone");
    }

    private static JsonObject With(JsonObject body, string path, string value)
    {
        string[] parts = path.Split('.');
        JsonObject target = body;
        foreach (string part in parts[..^1])
        {
            target = target[part]!.AsObject();
        }

        target[parts[^1]] = value;

        return body;
    }
}
