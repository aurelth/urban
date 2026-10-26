using System.Net;

namespace Urban.Api.Http;

/// <summary>
/// Quem a API aceita como proxy para ler o IP real de X-Forwarded-For (seção "TrustedProxies"). Sem isso toda requisição chegaria
/// com o IP do nginx e o limite por cliente valeria para todos juntos.
/// </summary>
public sealed class TrustedProxiesOptions
{
    public const string SectionName = "TrustedProxies";

    public const int MaxHops = 5;

    /// <summary>Redes em notação CIDR de onde vêm os proxies (nunca a internet). Os valores padrão ficam no appsettings.json.</summary>
    public string[] Networks { get; init; } = [];

    /// <summary>Quantos proxies existem entre o cliente e a API. Endereços além disso, escritos pelo cliente, são ignorados.</summary>
    public int Hops { get; init; }

    /// <exception cref="InvalidOperationException">A configuração é inválida.</exception>
    public void Validate()
    {
        if (Networks.Length == 0)
        {
            throw new InvalidOperationException($"Informe ao menos uma rede de proxy confiável ({SectionName}:{nameof(Networks)}).");
        }

        foreach (string network in Networks)
        {
            if (!IPNetwork.TryParse(network, out _))
            {
                throw new InvalidOperationException($"A rede '{network}' ({SectionName}:{nameof(Networks)}) precisa estar em notação CIDR, como 10.0.0.0/8.");
            }
        }

        if (Hops is < 1 or > MaxHops)
        {
            throw new InvalidOperationException($"O número de proxies ({SectionName}:{nameof(Hops)}) precisa estar entre 1 e {MaxHops}.");
        }
    }

    public IReadOnlyList<IPNetwork> ParseNetworks() => [.. Networks.Select(IPNetwork.Parse)];
}