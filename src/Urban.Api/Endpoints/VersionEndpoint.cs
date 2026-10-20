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
    // O .NET pode acrescentar "+<commit>" depois da versao; aqui ficamos so com a versao.
    private static string ReadVersion()
    {
        var informational = typeof(VersionEndpoint).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
