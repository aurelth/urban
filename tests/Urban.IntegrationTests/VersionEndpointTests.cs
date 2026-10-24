using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Urban.Api.Endpoints;
using Urban.IntegrationTests.Api;

namespace Urban.IntegrationTests;

public class VersionEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Get_api_version_deve_retornar_versao_e_ambiente()
    {
        var client = factory.CreateClient();

        var resposta = await client.GetAsync("/api/version");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<VersionResponse>();
        corpo.Should().NotBeNull();
        corpo!.Version.Should().NotBeNullOrWhiteSpace();
        corpo.Environment.Should().Be("Development");
    }
}