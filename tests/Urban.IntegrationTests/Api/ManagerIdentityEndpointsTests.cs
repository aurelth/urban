using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class ManagerIdentityEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string NewCpf = "11144477735";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Put_with_the_right_password_changes_name_birth_date_and_cpf_and_returns_the_cpf_masked()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        string version = await VersionAsync(manager);

        HttpResponseMessage response = await PutAsync(manager, Body(version));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string raw = await response.Content.ReadAsStringAsync(Ct);
        raw.Should().NotContain(NewCpf).And.NotContain(ManagerSetup.Password);
        JsonElement saved = JsonDocument.Parse(raw).RootElement;
        saved.GetProperty("manager").GetProperty("fullName").GetString().Should().Be("Maria Souza Lima");
        saved.GetProperty("manager").GetProperty("birthDate").GetString().Should().Be("1990-07-01");
        saved.GetProperty("manager").GetProperty("maskedCpf").GetString().Should().Be("***.444.777-**");
        saved.GetProperty("version").GetString().Should().NotBe(version);
        (await _client.ReadMineAsync(manager.AccessToken)).GetRawText().Should().Be(saved.GetRawText());
    }

    [Fact]
    public async Task Put_without_the_cpf_keeps_the_current_one()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject body = Body(await VersionAsync(manager));
        body.Remove("cpf");

        HttpResponseMessage response = await PutAsync(manager, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.ReadMineAsync(manager.AccessToken)).GetProperty("manager").GetProperty("maskedCpf").GetString().Should().Be("***.982.247-**");
    }

    [Fact]
    public async Task The_new_cpf_is_stored_encrypted_with_a_new_hash_for_the_duplicate_check()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        await using IdentityDbContext before = fixture.Postgres.CreateContext();
        string hashBefore = await before.Database
            .SqlQuery<string>($"""select cpf_hash as "Value" from condominium.person where user_id = {manager.User.Id}""").SingleAsync(Ct);

        (await PutAsync(manager, Body(await VersionAsync(manager)))).StatusCode.Should().Be(HttpStatusCode.OK);

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        string stored = await db.Database
            .SqlQuery<string>($"""select cpf_encrypted as "Value" from condominium.person where user_id = {manager.User.Id}""").SingleAsync(Ct);
        string hashAfter = await db.Database
            .SqlQuery<string>($"""select cpf_hash as "Value" from condominium.person where user_id = {manager.User.Id}""").SingleAsync(Ct);
        stored.Should().StartWith("v1:").And.NotContain(NewCpf);
        hashAfter.Should().NotBe(hashBefore).And.NotContain(NewCpf);
    }

    [Fact]
    public async Task A_wrong_password_is_403_and_nothing_is_saved()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonElement before = await _client.ReadMineAsync(manager.AccessToken);
        JsonObject body = Body(before.GetProperty("version").GetString()!);
        body["password"] = "Senha-errada-999";

        HttpResponseMessage response = await PutAsync(manager, body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.password.incorrect");
        (await _client.ReadMineAsync(manager.AccessToken)).GetRawText().Should().Be(before.GetRawText());
    }

    [Fact]
    public async Task A_missing_password_counts_as_a_wrong_one()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject body = Body(await VersionAsync(manager));
        body.Remove("password");

        HttpResponseMessage response = await PutAsync(manager, body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.password.incorrect");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_with_423()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        string version = await VersionAsync(manager);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            JsonObject wrong = Body(version);
            wrong["password"] = "Senha-errada-999";
            await PutAsync(manager, wrong);
        }

        HttpResponseMessage response = await PutAsync(manager, Body(version));

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await response.CodeAsync()).Should().Be(LoginErrors.LockedOut.Code);
    }

    [Theory]
    [InlineData("cpf", "111.111.111-11", "condominium.cpf.invalid")]
    [InlineData("birthDate", "2015-01-01", "condominium.birth_date.under_age")]
    [InlineData("fullName", "Ana", "condominium.person.full_name_invalid")]
    public async Task An_invalid_field_is_400_with_the_code_and_does_not_spend_a_password_attempt(string field, string value, string code)
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        JsonObject invalid = Body(await VersionAsync(manager));
        invalid[field] = value;

        for (int attempt = 0; attempt < 6; attempt++)
        {
            HttpResponseMessage rejected = await PutAsync(manager, invalid);
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await rejected.CodeAsync()).Should().Be(code);
        }

        (await PutAsync(manager, Body(await VersionAsync(manager)))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_stale_version_is_409()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        string stale = await VersionAsync(manager);
        (await PutAsync(manager, Body(stale))).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await PutAsync(manager, Body(stale));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("concurrency.conflict");
    }

    [Fact]
    public async Task Without_a_token_the_answer_is_401()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.IdentityRoute, null, Body("1.1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_manager_without_a_registered_condominium_gets_404()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.IdentityRoute, token, Body("1.1"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.not_found");
    }

    [Fact]
    public async Task The_log_entry_names_the_fields_and_never_the_values()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        (await PutAsync(manager, Body(await VersionAsync(manager)))).StatusCode.Should().Be(HttpStatusCode.OK);

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        string[] fields = await db.Database
            .SqlQuery<string>($"""select array_to_string(fields, ',') as "Value" from condominium.change_log where condominium_id = {manager.CondominiumId} and kind = 'identity'""")
            .ToArrayAsync(Ct);
        fields.Should().ContainSingle().Which.Should().Be("fullName,birthDate,cpf");
    }

    private static JsonObject Body(string version) => new()
    {
        ["version"] = version,
        ["password"] = ManagerSetup.Password,
        ["fullName"] = "Maria Souza Lima",
        ["birthDate"] = "1990-07-01",
        ["cpf"] = NewCpf,
    };

    private async Task<string> VersionAsync(ManagerSession manager) =>
        (await _client.ReadMineAsync(manager.AccessToken)).GetProperty("version").GetString()!;

    private Task<HttpResponseMessage> PutAsync(ManagerSession manager, JsonObject body) =>
        _client.SendJsonAsync(HttpMethod.Put, ManagerSetup.IdentityRoute, manager.AccessToken, body);
}
