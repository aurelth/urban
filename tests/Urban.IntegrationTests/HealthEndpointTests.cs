using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Urban.IntegrationTests;

public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_health_deve_retornar_200()
    {
        var client = factory.CreateClient();

        var resposta = await client.GetAsync("/health");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
