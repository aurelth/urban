using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class LogoutEndpointTests(ApiFixture fixture) : IDisposable
{
    private const string Password = "Senha-segura-123";
    private const string UnauthenticatedCode = "identity.session.unauthenticated";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Logout_returns_204_and_clears_the_refresh_cookie()
    {
        Session session = await SignInNewUserAsync();

        HttpResponseMessage response = await _client.LogoutAsync(session.AccessToken, session.RefreshCookie);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.RefreshCookieValue().Should().BeEmpty();
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_of_the_session()
    {
        Session session = await SignInNewUserAsync();
        await _client.LogoutAsync(session.AccessToken, session.RefreshCookie);

        HttpResponseMessage refresh = await _client.RefreshAsync(session.RefreshCookie);

        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refresh.CodeAsync()).Should().Be("identity.refresh_token.reused");
    }

    [Fact]
    public async Task Logout_without_a_refresh_cookie_still_succeeds()
    {
        Session session = await SignInNewUserAsync();

        HttpResponseMessage response = await _client.LogoutAsync(session.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_access_token_is_rejected_after_logout()
    {
        Session session = await SignInNewUserAsync();
        await _client.LogoutAsync(session.AccessToken);

        HttpResponseMessage again = await _client.LogoutAsync(session.AccessToken);

        again.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await again.CodeAsync()).Should().Be(UnauthenticatedCode);
    }

    [Fact]
    public async Task Logout_without_a_token_returns_401_with_a_bearer_challenge()
    {
        HttpResponseMessage response = await _client.LogoutAsync(accessToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle(h => h.Scheme == "Bearer");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.CodeAsync()).Should().Be(UnauthenticatedCode);
    }

    [Fact]
    public async Task Logout_accepts_a_token_signed_with_the_right_key()
    {
        HttpResponseMessage response = await _client.LogoutAsync(ForgedTokens.Create(fixture.Factory.SigningKey));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unsigned")]
    [InlineData("expired")]
    [InlineData("no-expiration")]
    [InlineData("other-key")]
    [InlineData("other-algorithm")]
    [InlineData("other-issuer")]
    [InlineData("other-audience")]
    public async Task Logout_rejects_tokens_this_api_did_not_issue(string kind)
    {
        HttpResponseMessage response = await _client.LogoutAsync(Forge(kind));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.CodeAsync()).Should().Be(UnauthenticatedCode);
    }

    [Fact]
    public async Task Tokens_issued_before_a_full_revocation_are_rejected_and_later_ones_accepted()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        Session before = await SignInAsync(user);

        await fixture.Factory.Services.GetRequiredService<ISessionRevocations>()
            .RevokeAllOfUserAsync(user.Id, DateTimeOffset.UtcNow, CancellationToken.None);
        HttpResponseMessage rejected = await _client.LogoutAsync(before.AccessToken);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        Session after = await SignInAsync(user);
        HttpResponseMessage accepted = await _client.LogoutAsync(after.AccessToken);

        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_access_tokens_of_the_user()
    {
        Session session = await SignInNewUserAsync();
        await _client.RefreshAsync(session.RefreshCookie);
        await _client.RefreshAsync(session.RefreshCookie);

        HttpResponseMessage response = await _client.LogoutAsync(session.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_invalid_bearer_header_does_not_block_public_routes()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = user.Email.Value, password = Password }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "lixo");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_answers_503_when_the_session_store_is_down_while_login_keeps_working()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        await using ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = ApiFactory.DownRedis,
        };
        using HttpClient client = factory.CreateClient(new() { HandleCookies = false });

        HttpResponseMessage login = await client.LoginAsync(user.Email.Value, Password);
        HttpResponseMessage logout = await client.LogoutAsync(await login.AccessTokenAsync());

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        logout.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await logout.CodeAsync()).Should().Be("identity.session.store_unavailable");
    }

    private string Forge(string kind)
    {
        string key = fixture.Factory.SigningKey;

        return kind switch
        {
            "malformed" => "isto-nao-e-um-jwt",
            "unsigned" => ForgedTokens.Unsigned(),
            "expired" => ForgedTokens.Create(key, expired: true),
            "no-expiration" => ForgedTokens.WithoutExpiration(key),
            "other-key" => ForgedTokens.Create(ForgedTokens.NewSigningKey()),
            "other-algorithm" => ForgedTokens.Create(key, SecurityAlgorithms.HmacSha512),
            "other-issuer" => ForgedTokens.Create(key, issuer: "outro"),
            "other-audience" => ForgedTokens.Create(key, audience: "outro"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private async Task<Session> SignInNewUserAsync() =>
        await SignInAsync(await fixture.Postgres.SeedUserWithPasswordAsync(Password));

    private async Task<Session> SignInAsync(User user)
    {
        HttpResponseMessage login = await _client.LoginAsync(user.Email.Value, Password);

        return new Session(await login.AccessTokenAsync(), login.RefreshCookieValue()!);
    }

    private sealed record Session(string AccessToken, string RefreshCookie);
}