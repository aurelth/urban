using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Urban.Identity.Application.Abstractions;

namespace Urban.Identity.Infrastructure.Tokens;

/// <summary>
/// Segredo de 256 bits vindo do gerador criptográfico do sistema. O hash é SHA-256 simples: como o segredo já é
/// aleatório e longo, não há o que adivinhar, então não precisa de sal nem de hash lento (isso é para senhas).
/// </summary>
public sealed class SecretGenerator : ISecretGenerator
{
    private const int SecretBytes = 32;

    public GeneratedSecret Generate()
    {
        string value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        return new GeneratedSecret(value, Hash(value));
    }

    public string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
