namespace Urban.Api.Http;

/// <summary>Limite de uma rota: no máximo <see cref="PermitLimit"/> requisições por <see cref="Window"/>, para cada cliente.</summary>
public sealed class RateLimitRule
{
    public int PermitLimit { get; init; }

    public TimeSpan Window { get; init; }
}