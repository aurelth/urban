using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Urban.Api.Endpoints;

namespace Urban.IntegrationTests;

public class VersionEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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
