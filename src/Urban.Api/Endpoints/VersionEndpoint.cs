using System.Reflection;

namespace Urban.Api.Endpoints;

/// <summary>Resposta do endpoint GET /api/version.</summary>
public sealed record VersionResponse(string Version, string Environment);

public static class VersionEndpoint
{
    /// <summary>Mapeia GET /api/version: versao da API e ambiente em que ela roda.</summary>
    public static IEndpointRouteBuilder MapVersionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/version", (IHostEnvironment env) => new VersionResponse(ReadVersion(), env.EnvironmentName));
        return app;
    }

    // A versao vem de -p:Version no build da imagem (Release: 0.1.0, staging: 0.0.0-sha-abc1234).
    private static string ReadVersion() =>
        NormalizeVersion(typeof(VersionEndpoint).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Remove o sufixo "+commit" que o .NET pode acrescentar depois da versao.
    /// Sem versao informada, devolve 0.0.0.
    /// </summary>
    public static string NormalizeVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "0.0.0";
        }

        int plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informationalVersion : informationalVersion[..plus];
    }
}
