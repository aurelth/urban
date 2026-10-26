using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class MeEndpointTests(ApiFixture fixture) : IDisposable
{
    private const string Password = "Senha-segura-123";
    private const string UnauthenticatedCode = "identity.session.unauthenticated";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Me_returns_who_is_signed_in_and_nothing_more()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password, role: UserRole.Manager);

        HttpResponseMessage response = await _client.MeAsync(await SignInAsync(user));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "email", "role", "condominiumId");
        body.GetProperty("id").GetGuid().Should().Be(user.Id);
        body.GetProperty("email").GetString().Should().Be(user.Email.Value);
        body.GetProperty("role").GetString().Should().Be("Manager");
        body.GetProperty("condominiumId").GetGuid().Should().Be(user.CondominiumId!.Value);
    }

    [Fact]
    public async Task Me_of_a_super_admin_has_no_condominium()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password, role: UserRole.SuperAdmin);

        HttpResponseMessage response = await _client.MeAsync(await SignInAsync(user));

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("role").GetString().Should().Be("SuperAdmin");
        body.GetProperty("condominiumId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Me_is_never_cached()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        HttpResponseMessage response = await _client.MeAsync(await SignInAsync(user));

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Pragma.ToString().Should().Be("no-cache");
    }

    [Fact]
    public async Task Me_without_a_token_returns_401_with_a_bearer_challenge()
    {
        HttpResponseMessage response = await _client.MeAsync(accessToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle(h => h.Scheme == "Bearer");
        (await response.CodeAsync()).Should().Be(UnauthenticatedCode);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unsigned")]
    [InlineData("expired")]
    [InlineData("other-key")]
    public async Task Me_rejects_tokens_this_api_did_not_issue(string kind)
    {
        HttpResponseMessage response = await _client.MeAsync(Forge(kind));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.CodeAsync()).Should().Be(UnauthenticatedCode);
    }

    [Fact]
    public async Task Me_rejects_a_valid_token_of_a_user_that_does_not_exist()
    {
        HttpResponseMessage response = await _client.MeAsync(ForgedTokens.Create(fixture.Factory.SigningKey));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.CodeAsync()).Should().Be(UnauthenticatedCode);
    }

    [Fact]
    public async Task Me_rejects_the_access_token_after_logout()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        string accessToken = await SignInAsync(user);
        await _client.LogoutAsync(accessToken);

        HttpResponseMessage response = await _client.MeAsync(accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_rejects_tokens_issued_before_a_full_revocation()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        string accessToken = await SignInAsync(user);
        await fixture.Factory.Services.GetRequiredService<ISessionRevocations>()
            .RevokeAllOfUserAsync(user.Id, DateTimeOffset.UtcNow, CancellationToken.None);

        HttpResponseMessage response = await _client.MeAsync(accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("suspend", "identity.user.account_suspended")]
    [InlineData("deactivate", "identity.user.account_deactivated")]
    public async Task Me_reveals_the_state_of_an_account_that_lost_access_after_signing_in(string action, string code)
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        string accessToken = await SignInAsync(user);
        await ChangeStateAsync(user.Id, action);

        HttpResponseMessage response = await _client.MeAsync(accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be(code);
    }

    private string Forge(string kind)
    {
        string key = fixture.Factory.SigningKey;

        return kind switch
        {
            "malformed" => "isto-nao-e-um-jwt",
            "unsigned" => ForgedTokens.Unsigned(),
            "expired" => ForgedTokens.Create(key, expired: true),
            "other-key" => ForgedTokens.Create(ForgedTokens.NewSigningKey()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private async Task<string> SignInAsync(User user)
    {
        HttpResponseMessage login = await _client.LoginAsync(user.Email.Value, Password);

        return await login.AccessTokenAsync();
    }

    private async Task ChangeStateAsync(Guid userId, string action)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.SingleAsync(u => u.Id == userId);
        Guid actor = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (action == "suspend")
        {
            user.Suspend(actor, "Motivo de teste", now);
        }
        else
        {
            user.Deactivate(actor, now);
        }

        await db.SaveChangesAsync();
    }
}