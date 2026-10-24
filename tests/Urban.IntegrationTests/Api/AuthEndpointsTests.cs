using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class AuthEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Password = "Senha-segura-123";
    private const string RefreshCookieName = "__Secure-urban-refresh";

    // Cookies tratados à mão: o cookie é Secure e o cliente de teste fala HTTP, então o contêiner de cookies não o enviaria.
    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Login_with_valid_credentials_returns_access_token_in_the_body()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        HttpResponseMessage response = await LoginAsync(user.Email.Value, Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        string accessToken = body.GetProperty("accessToken").GetString()!;
        JsonWebToken jwt = new JsonWebTokenHandler().ReadJsonWebToken(accessToken);
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.GetClaim("role").Value.Should().Be("Resident");
        body.GetProperty("accessTokenExpiresAt").GetDateTimeOffset()
            .Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Login_never_puts_the_refresh_token_in_the_body()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        HttpResponseMessage response = await LoginAsync(user.Email.Value, Password);

        string raw = await response.Content.ReadAsStringAsync();
        string refreshValue = CookieValue(response)!;
        raw.Should().NotContain(refreshValue);
        raw.ToLowerInvariant().Should().NotContain("refresh");
    }

    [Fact]
    public async Task Login_sets_a_locked_down_refresh_cookie_scoped_to_the_auth_path()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        HttpResponseMessage response = await LoginAsync(user.Email.Value, Password);

        // Só os atributos: o nome do cookie também contém a palavra "secure".
        string[] attributes = SetCookieHeader(response)!.ToLowerInvariant().Split(';').Skip(1).Select(a => a.Trim()).ToArray();
        attributes.Should().Contain("httponly");
        attributes.Should().Contain("secure");
        attributes.Should().Contain("samesite=strict");
        attributes.Should().Contain("path=/api/auth");
        attributes.Should().Contain(a => a.StartsWith("expires=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_response_is_not_cacheable()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        HttpResponseMessage response = await LoginAsync(user.Email.Value, Password);

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_without_cookie()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        HttpResponseMessage response = await LoginAsync(user.Email.Value, "outra-senha-999");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(response)).Should().Be("identity.login.invalid_credentials");
        SetCookieHeader(response).Should().BeNull();
    }

    [Fact]
    public async Task Login_answers_the_same_for_unknown_email_and_wrong_password()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        HttpResponseMessage wrongPassword = await LoginAsync(user.Email.Value, "outra-senha-999");
        HttpResponseMessage unknownEmail = await LoginAsync($"{Guid.NewGuid():N}@example.com", Password);

        unknownEmail.StatusCode.Should().Be(wrongPassword.StatusCode);
        JsonElement a = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement b = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        b.GetProperty("code").GetString().Should().Be(a.GetProperty("code").GetString());
        b.GetProperty("detail").GetString().Should().Be(a.GetProperty("detail").GetString());
        b.GetProperty("title").GetString().Should().Be(a.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_with_missing_fields_returns_401()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(response)).Should().Be("identity.login.invalid_credentials");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_with_423_even_for_the_right_password()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            (await LoginAsync(user.Email.Value, "outra-senha-999")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        HttpResponseMessage fifth = await LoginAsync(user.Email.Value, "outra-senha-999");
        HttpResponseMessage rightPassword = await LoginAsync(user.Email.Value, Password);

        fifth.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await CodeOf(fifth)).Should().Be("identity.login.locked_out");
        rightPassword.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification, "identity.user.email_not_verified")]
    [InlineData(AccountStatus.Deactivated, "identity.user.account_deactivated")]
    [InlineData(AccountStatus.Suspended, "identity.user.account_suspended")]
    public async Task Login_reveals_the_account_state_only_after_the_right_password(AccountStatus status, string code)
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password, status);

        HttpResponseMessage rightPassword = await LoginAsync(user.Email.Value, Password);
        HttpResponseMessage wrongPassword = await LoginAsync(user.Email.Value, "outra-senha-999");

        rightPassword.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOf(rightPassword)).Should().Be(code);
        SetCookieHeader(rightPassword).Should().BeNull();
        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_the_cookie_rotates_the_token_and_returns_a_new_access_token()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        HttpResponseMessage login = await LoginAsync(user.Email.Value, Password);
        string oldCookie = CookieValue(login)!;

        HttpResponseMessage refresh = await RefreshAsync(oldCookie);

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("accessToken").GetString()).Subject
            .Should().Be(user.Id.ToString());
        string newCookie = CookieValue(refresh)!;
        newCookie.Should().NotBe(oldCookie);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401_and_clears_the_cookie()
    {
        HttpResponseMessage response = await RefreshAsync(cookie: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(response)).Should().Be("identity.refresh.invalid_session");
        CookieValue(response).Should().BeEmpty();
        SetCookieHeader(response)!.ToLowerInvariant().Should().Contain("path=/api/auth");
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_returns_401()
    {
        HttpResponseMessage response = await RefreshAsync("token-que-nunca-existiu");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(response)).Should().Be("identity.refresh.invalid_session");
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_returns_401_and_kills_the_whole_family()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password);
        string first = CookieValue(await LoginAsync(user.Email.Value, Password))!;
        string second = CookieValue(await RefreshAsync(first))!;

        HttpResponseMessage reuse = await RefreshAsync(first);
        HttpResponseMessage legitimateHolder = await RefreshAsync(second);

        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(reuse)).Should().Be("identity.refresh_token.reused");
        CookieValue(reuse).Should().BeEmpty();
        legitimateHolder.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private Task<HttpResponseMessage> RefreshAsync(string? cookie)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/refresh");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={cookie}");
        }

        return _client.SendAsync(request);
    }

    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    private static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.SingleOrDefault(v => v.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal))
            : null;

    private static string? CookieValue(HttpResponseMessage response)
    {
        string? header = SetCookieHeader(response);
        return header is null ? null : header[(RefreshCookieName.Length + 1)..].Split(';')[0];
    }
}