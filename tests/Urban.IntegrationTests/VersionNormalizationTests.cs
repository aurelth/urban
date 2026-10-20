using FluentAssertions;
using Urban.Api.Endpoints;

namespace Urban.IntegrationTests;

public class VersionNormalizationTests
{
    [Theory]
    [InlineData(null, "0.0.0")]
    [InlineData("", "0.0.0")]
    [InlineData("   ", "0.0.0")]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("0.1.0+4e63da2", "0.1.0")]
    [InlineData("0.0.0-sha-3d15049", "0.0.0-sha-3d15049")]
    public void NormalizeVersion_deve_remover_o_sufixo_de_commit(string? entrada, string esperado)
    {
        VersionEndpoint.NormalizeVersion(entrada).Should().Be(esperado);
    }
}
