using System.Net;
using FluentAssertions;
using Urban.IntegrationTests.Api;

namespace Urban.IntegrationTests;

public class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Get_health_deve_retornar_200()
    {
        var client = factory.CreateClient();

        var resposta = await client.GetAsync("/health");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}