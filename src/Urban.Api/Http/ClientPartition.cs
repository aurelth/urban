using System.Net;
using System.Net.Sockets;

namespace Urban.Api.Http;

/// <summary>Decide quem é "o mesmo cliente" para o limite de requisições.</summary>
public static class ClientPartition
{
    public const string Unknown = "desconhecido";

    private const int Ipv6NetworkBytes = 8;

    /// <summary>
    /// IPv4 vale por inteiro. No IPv6 vale o prefixo de 64 bits, porque quem recebe um endereço IPv6 costuma receber uma rede /64
    /// inteira e trocar de endereço dentro dela é trivial: contar por endereço completo deixaria o limite sem efeito.
    /// </summary>
    public static string KeyOf(IPAddress? address)
    {
        if (address is null)
        {
            return Unknown;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[Ipv6NetworkBytes..].Clear();

        return $"{new IPAddress(bytes)}/64";
    }
}