using System.Security.Cryptography;
using System.Text;
using Urban.Condominium.Application.Abstractions;

namespace Urban.Condominium.Infrastructure.Security;

/// <summary>
/// AES-256-GCM para cifrar (autenticado: qualquer alteração é detectada) e HMAC-SHA256 para o hash. O valor guardado é
/// <c>{idDaChave}:{base64(nonce | cifrado | etiqueta)}</c>, com nonce aleatório de 96 bits em cada cifragem.
/// </summary>
public sealed class PersonalDataProtector : IPersonalDataProtector
{
    private const int NonceBytes = 12;

    private const int TagBytes = 16;

    private const char Separator = ':';

    // Amarra o cifrado a este uso: o mesmo texto cifrado em outro lugar não seria aceito aqui.
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("urban:personal-data");

    private readonly string _currentKeyId;

    private readonly IReadOnlyDictionary<string, byte[]> _encryptionKeys;

    private readonly byte[] _hashKey;

    public PersonalDataProtector(PersonalDataOptions options)
    {
        _encryptionKeys = options.GetEncryptionKeys();
        _hashKey = options.GetHashKey();
        _currentKeyId = options.CurrentKeyId;
    }

    public string Protect(string plaintext)
    {
        byte[] plain = Encoding.UTF8.GetBytes(plaintext);
        byte[] payload = new byte[NonceBytes + plain.Length + TagBytes];
        Span<byte> nonce = payload.AsSpan(0, NonceBytes);
        RandomNumberGenerator.Fill(nonce);

        using AesGcm aes = new(_encryptionKeys[_currentKeyId], TagBytes);
        aes.Encrypt(nonce, plain, payload.AsSpan(NonceBytes, plain.Length), payload.AsSpan(NonceBytes + plain.Length, TagBytes), AssociatedData);

        return $"{_currentKeyId}{Separator}{Convert.ToBase64String(payload)}";
    }

    public string Unprotect(string protectedValue)
    {
        int separator = protectedValue.IndexOf(Separator, StringComparison.Ordinal);
        if (separator <= 0 || !_encryptionKeys.TryGetValue(protectedValue[..separator], out byte[]? key))
        {
            throw new CryptographicException("O valor protegido não tem uma chave conhecida.");
        }

        byte[] payload = DecodePayload(protectedValue[(separator + 1)..]);
        int cipherLength = payload.Length - NonceBytes - TagBytes;
        if (cipherLength < 0)
        {
            throw new CryptographicException("O valor protegido está truncado.");
        }

        byte[] plain = new byte[cipherLength];
        using AesGcm aes = new(key, TagBytes);
        aes.Decrypt(
            payload.AsSpan(0, NonceBytes),
            payload.AsSpan(NonceBytes, cipherLength),
            payload.AsSpan(NonceBytes + cipherLength, TagBytes),
            plain,
            AssociatedData);

        return Encoding.UTF8.GetString(plain);
    }

    public string Hash(string value) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(value)));

    private static byte[] DecodePayload(string base64)
    {
        byte[] buffer = new byte[base64.Length];
        return Convert.TryFromBase64String(base64, buffer, out int written)
            ? buffer[..written]
            : throw new CryptographicException("O valor protegido não é base64.");
    }
}
