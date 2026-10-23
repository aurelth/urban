namespace Urban.Identity.Infrastructure.Tokens;

/// <summary>Configuração do JWT (seção "Jwt"). A chave de assinatura vem do ambiente, nunca do repositório.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; init; } = "urban";

    public string Audience { get; init; } = "urban";

    /// <summary>Chave simétrica em base64, com pelo menos 32 bytes (256 bits).</summary>
    public string SigningKey { get; init; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public byte[] GetSigningKeyBytes()
    {
        Span<byte> buffer = new byte[SigningKey.Length];
        bool isBase64 = Convert.TryFromBase64String(SigningKey, buffer, out int written);

        return isBase64 && written >= MinimumSigningKeyBytes
            ? buffer[..written].ToArray()
            : throw new InvalidOperationException(
                $"A chave de assinatura do JWT deve ser base64 com pelo menos {MinimumSigningKeyBytes} bytes.");
    }
}
