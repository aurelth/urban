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