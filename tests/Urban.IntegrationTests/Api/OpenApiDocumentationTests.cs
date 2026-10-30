using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Urban.IntegrationTests.Api;

public sealed class OpenApiDocumentationTests
{
    [Fact]
    public async Task Development_serves_the_contract_with_the_auth_endpoints()
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement paths = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paths");
        paths.TryGetProperty("/api/auth/login", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/refresh", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/register", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/confirm-email", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/forgot-password", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/reset-password", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/me", out _).Should().BeTrue();
        paths.TryGetProperty("/api/terms/current", out _).Should().BeTrue();
        paths.TryGetProperty("/api/condominiums", out _).Should().BeTrue();
        paths.TryGetProperty("/api/condominiums/mine", out _).Should().BeTrue();
        paths.TryGetProperty("/api/condominiums/mine/manager-identity", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/change-password", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/change-email", out _).Should().BeTrue();
        paths.TryGetProperty("/api/auth/confirm-email-change", out _).Should().BeTrue();
        paths.TryGetProperty("/api/postal-codes/{postalCode}", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Development_documents_me_as_protected_and_the_current_terms_as_open()
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        JsonElement paths = (await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");

        JsonElement me = paths.GetProperty("/api/auth/me").GetProperty("get");
        me.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
        foreach (string status in new[] { "200", "401", "403" })
        {
            me.GetProperty("responses").TryGetProperty(status, out _).Should().BeTrue(status);
        }

        paths.GetProperty("/api/terms/current").GetProperty("get").TryGetProperty("security", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Development_documents_logout_as_protected_by_a_bearer_token()
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        JsonElement document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        document.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).Should().BeTrue();
        JsonElement logout = document.GetProperty("paths").GetProperty("/api/auth/logout").GetProperty("post");
        logout.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
        foreach (string open in new[] { "login", "register", "confirm-email", "forgot-password", "reset-password" })
        {
            document.GetProperty("paths").GetProperty($"/api/auth/{open}").GetProperty("post").TryGetProperty("security", out _)
                .Should().BeFalse(open);
        }
    }

    [Theory]
    [InlineData("/api/auth/login", "post")]
    [InlineData("/api/auth/refresh", "post")]
    [InlineData("/api/auth/logout", "post")]
    [InlineData("/api/auth/me", "get")]
    [InlineData("/api/auth/register", "post")]
    [InlineData("/api/auth/confirm-email", "post")]
    [InlineData("/api/auth/forgot-password", "post")]
    [InlineData("/api/auth/reset-password", "post")]
    [InlineData("/api/terms/current", "get")]
    [InlineData("/api/condominiums", "post")]
    [InlineData("/api/condominiums/mine", "get")]
    [InlineData("/api/condominiums/mine", "put")]
    [InlineData("/api/condominiums/mine/manager-identity", "put")]
    [InlineData("/api/auth/change-password", "post")]
    [InlineData("/api/auth/change-email", "post")]
    [InlineData("/api/auth/confirm-email-change", "post")]
    [InlineData("/api/postal-codes/{postalCode}", "get")]
    public async Task Development_documents_the_429_of_every_rate_limited_route(string path, string method)
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        JsonElement paths = (await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");

        paths.GetProperty(path).GetProperty(method).GetProperty("responses").TryGetProperty("429", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Development_documents_the_condominium_registration_as_protected_with_every_outcome()
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        JsonElement paths = (await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");

        JsonElement post = paths.GetProperty("/api/condominiums").GetProperty("post");
        post.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
        foreach (string status in new[] { "201", "400", "401", "403", "409", "429" })
        {
            post.GetProperty("responses").TryGetProperty(status, out _).Should().BeTrue(status);
        }
    }

    [Theory]
    [InlineData("/api/condominiums/mine", "get", "200,401,403,404,429")]
    [InlineData("/api/condominiums/mine", "put", "200,400,401,403,404,409,429")]
    [InlineData("/api/condominiums/mine/manager-identity", "put", "200,400,401,403,404,409,423,429")]
    [InlineData("/api/auth/change-password", "post", "204,400,401,403,423,429")]
    [InlineData("/api/auth/change-email", "post", "202,400,401,403,423,429")]
    public async Task Development_documents_the_editing_routes_as_protected_with_every_outcome(string path, string method, string statuses)
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        JsonElement paths = (await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");

        JsonElement operation = paths.GetProperty(path).GetProperty(method);
        operation.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
        foreach (string status in statuses.Split(','))
        {
            operation.GetProperty("responses").TryGetProperty(status, out _).Should().BeTrue(status);
        }
    }

    [Fact]
    public async Task Development_documents_the_email_change_confirmation_as_open_with_its_outcomes()
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        JsonElement paths = (await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");

        JsonElement post = paths.GetProperty("/api/auth/confirm-email-change").GetProperty("post");
        post.TryGetProperty("security", out _).Should().BeFalse();
        foreach (string status in new[] { "204", "400", "429" })
        {
            post.GetProperty("responses").TryGetProperty(status, out _).Should().BeTrue(status);
        }
    }

    [Fact]
    public async Task Development_documents_the_postal_code_lookup_as_protected_with_every_outcome()
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        JsonElement paths = (await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");

        JsonElement get = paths.GetProperty("/api/postal-codes/{postalCode}").GetProperty("get");
        get.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
        foreach (string status in new[] { "200", "400", "401", "404", "429", "503" })
        {
            get.GetProperty("responses").TryGetProperty(status, out _).Should().BeTrue(status);
        }
    }

    [Fact]
    public async Task Development_serves_the_scalar_page()
    {
        await using ApiFactory factory = new() { EnvironmentName = "Development" };
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task Production_does_not_expose_the_documentation(string path)
    {
        await using ApiFactory factory = new() { EnvironmentName = "Production" };
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}