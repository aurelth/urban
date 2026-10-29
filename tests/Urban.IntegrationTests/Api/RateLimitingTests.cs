using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class RateLimitingTests(ApiFixture fixture)
{
    private const int Limit = 3;

    private const string RegisterRoute = "/api/auth/register";

    private const string RefreshRoute = "/api/auth/refresh";

    private const string LogoutRoute = "/api/auth/logout";

    private const string MeRoute = "/api/auth/me";

    private const string TermsRoute = "/api/terms/current";

    private static readonly IPAddress TrustedProxy = IPAddress.Parse("10.0.0.2");

    private static readonly IPAddress PublicAddress = IPAddress.Parse("198.51.100.7");

    [Theory]
    [InlineData(RegisterRoute)]
    [InlineData("/api/auth/confirm-email")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/forgot-password")]
    [InlineData("/api/auth/reset-password")]
    [InlineData(RefreshRoute)]
    [InlineData(LogoutRoute)]
    [InlineData(MeRoute)]
    [InlineData(TermsRoute)]
    public async Task Blocks_a_client_that_exceeds_the_limit_of_the_route(string route)
    {
        await using ApiFactory factory = CreateFactory(TimeSpan.FromMinutes(1));
        using HttpClient client = factory.CreateClient();

        await ExhaustAsync(client, route);
        HttpResponseMessage blocked = await SendAsync(client, route);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await blocked.CodeAsync()).Should().Be("rate_limit.exceeded");
        blocked.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData(RegisterRoute, 1)]
    [InlineData("/api/auth/confirm-email", 2)]
    [InlineData("/api/auth/login", 3)]
    [InlineData("/api/auth/forgot-password", 4)]
    [InlineData("/api/auth/reset-password", 5)]
    [InlineData(RefreshRoute, 6)]
    [InlineData(LogoutRoute, 7)]
    [InlineData(MeRoute, 8)]
    [InlineData(TermsRoute, 9)]
    public async Task Applies_the_limit_configured_for_that_route(string route, int permits)
    {
        Dictionary<string, string?> limits = new()
        {
            ["RateLimiting:Register:PermitLimit"] = "1",
            ["RateLimiting:ConfirmEmail:PermitLimit"] = "2",
            ["RateLimiting:Login:PermitLimit"] = "3",
            ["RateLimiting:ForgotPassword:PermitLimit"] = "4",
            ["RateLimiting:ResetPassword:PermitLimit"] = "5",
            ["RateLimiting:Refresh:PermitLimit"] = "6",
            ["RateLimiting:Logout:PermitLimit"] = "7",
            ["RateLimiting:Me:PermitLimit"] = "8",
            ["RateLimiting:Terms:PermitLimit"] = "9",
        };
        await using ApiFactory factory = CreateFactory(TimeSpan.FromMinutes(1), settings: limits);
        using HttpClient client = factory.CreateClient();

        await ExhaustAsync(client, route, permits);
        HttpResponseMessage blocked = await SendAsync(client, route);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData(RegisterRoute, 5)]
    [InlineData("/api/auth/confirm-email", 10)]
    [InlineData("/api/auth/login", 10)]
    [InlineData("/api/auth/forgot-password", 5)]
    [InlineData("/api/auth/reset-password", 10)]
    [InlineData(RefreshRoute, 30)]
    [InlineData(LogoutRoute, 20)]
    [InlineData(MeRoute, 60)]
    [InlineData(TermsRoute, 30)]
    public async Task Ships_with_the_limits_of_the_appsettings(string route, int permits)
    {
        await using ApiFactory factory = CreateFactory(TimeSpan.FromMinutes(1), limit: null);
        using HttpClient client = factory.CreateClient();

        await ExhaustAsync(client, route, permits);
        HttpResponseMessage blocked = await SendAsync(client, route);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Counts_each_route_on_its_own()
    {
        await using ApiFactory factory = CreateFactory(TimeSpan.FromMinutes(1));
        using HttpClient client = factory.CreateClient();
        await ExhaustAsync(client, RegisterRoute);

        HttpResponseMessage login = await SendAsync(client, "/api/auth/login");

        login.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Allows_requests_again_when_the_window_ends()
    {
        await using ApiFactory factory = CreateFactory(TimeSpan.FromSeconds(1), limit: 1);
        using HttpClient client = factory.CreateClient();
        await ExhaustAsync(client, RegisterRoute, limit: 1);

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        HttpResponseMessage afterWindow = await SendAsync(client, RegisterRoute);

        afterWindow.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Counts_each_client_behind_the_proxies_separately()
    {
        await using ApiFactory factory = CreateFactory(TimeSpan.FromMinutes(1), remote: TrustedProxy);
        using HttpClient client = factory.CreateClient();
        await ExhaustAsync(client, RegisterRoute, forwardedFor: _ => "203.0.113.10, 172.18.0.1");

        HttpResponseMessage sameClient = await SendAsync(client, RegisterRoute, "203.0.113.10, 172.18.0.1");
        HttpResponseMessage otherClient = await SendAsync(client, RegisterRoute, "203.0.113.11, 172.18.0.1");

        sameClient.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherClient.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Ignores_forged_addresses_the_client_puts_before_the_proxies()
    {
        await using ApiFactory factory = CreateFactory(TimeSpan.FromMinutes(1), remote: TrustedProxy);
        using HttpClient client = factory.CreateClient();
        await ExhaustAsync(client, RegisterRoute, forwardedFor: i => $"198.51.100.{i}, 203.0.113.20, 172.18.0.1");

        HttpResponseMessage forgedAgain = await SendAsync(client, RegisterRoute, "192.0.2.99, 203.0.113.20, 172.18.0.1");

        forgedAgain.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Ignores_forwarding_headers_from_a_connection_that_is_not_a_trusted_proxy()
    {
        await using ApiFactory factory = CreateFactory(TimeSpan.FromMinutes(1), remote: PublicAddress);
        using HttpClient client = factory.CreateClient();
        await ExhaustAsync(client, RegisterRoute, forwardedFor: i => $"203.0.113.{i}");

        HttpResponseMessage anotherClaim = await SendAsync(client, RegisterRoute, "203.0.113.99");

        anotherClaim.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static async Task ExhaustAsync(HttpClient client, string route, int limit = Limit, Func<int, string>? forwardedFor = null)
    {
        for (int i = 0; i < limit; i++)
        {
            HttpResponseMessage response = await SendAsync(client, route, forwardedFor?.Invoke(i));
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, $"a requisição {i + 1} de {limit} ainda cabe no limite");
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string route, string? forwardedFor = null)
    {
        using HttpRequestMessage request = new(IsRead(route) ? HttpMethod.Get : HttpMethod.Post, route);
        if (HasBody(route))
        {
            request.Content = JsonContent.Create(BodyFor(route));
        }

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request);
    }

    private static bool IsRead(string route) => route is MeRoute or TermsRoute;

    private static bool HasBody(string route) => route is not (MeRoute or TermsRoute or RefreshRoute or LogoutRoute);

    private static object BodyFor(string route) => route switch
    {
        RegisterRoute => new { email = "invalido" },
        "/api/auth/confirm-email" or "/api/auth/reset-password" => new { token = string.Empty, password = string.Empty },
        "/api/auth/forgot-password" => new { email = "invalido" },
        _ => new { email = "ninguem@example.com", password = "qualquer-senha" },
    };

    private ApiFactory CreateFactory(
        TimeSpan window,
        int? limit = Limit,
        IPAddress? remote = null,
        IReadOnlyDictionary<string, string?>? settings = null) => new()
    {
        PostgresConnectionString = fixture.Postgres.ConnectionString,
        RedisConnectionString = fixture.Factory.RedisConnectionString,
        RateLimitPermitLimit = limit,
        RateLimitWindow = window,
        RemoteAddress = remote,
        Settings = settings ?? new Dictionary<string, string?>(),
    };
}