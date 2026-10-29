using System.Text.RegularExpressions;

namespace Urban.Condominium.Infrastructure.Security;

/// <summary>
/// Chaves que protegem os dados pessoais (seção <c>PersonalData</c>, vinda dos secrets do ambiente). Cada chave de
/// cifragem tem um identificador (<c>v1</c>, <c>v2</c>...), que vai na frente do valor guardado: assim, para girar a
/// chave, basta incluir a nova, torná-la a atual e manter as antigas até tudo ser recifrado.
/// </summary>
public sealed partial class PersonalDataOptions
{
    public const string SectionName = "PersonalData";

    public const int EncryptionKeyBytes = 32;

    public const int MinimumHashKeyBytes = 32;

    /// <summary>Identificador da chave que cifra os valores novos.</summary>
    public string CurrentKeyId { get; init; } = "v1";

    /// <summary>Identificador (minúsculas, algarismos e hífen, até 16 caracteres) para base64 de 32 bytes.</summary>
    public Dictionary<string, string> EncryptionKeys { get; init; } = [];

    /// <summary>Chave do HMAC que gera o hash do CPF; precisa ser diferente de todas as chaves de cifragem.</summary>
    public string HashKey { get; init; } = string.Empty;

    /// <exception cref="InvalidOperationException">Chaves ausentes ou inválidas. A mensagem nomeia a configuração, nunca o valor.</exception>
    public IReadOnlyDictionary<string, byte[]> GetEncryptionKeys()
    {
        if (EncryptionKeys.Count == 0)
        {
            throw new InvalidOperationException($"Falta a configuração {SectionName}:EncryptionKeys (ao menos uma chave de cifragem dos dados pessoais).");
        }

        Dictionary<string, byte[]> keys = [];
        foreach ((string id, string value) in EncryptionKeys)
        {
            if (!KeyIdPattern().IsMatch(id))
            {
                throw new InvalidOperationException(
                    $"O identificador de chave '{id}' em {SectionName}:EncryptionKeys é inválido: use minúsculas, algarismos e hífen, de 1 a 16 caracteres.");
            }

            keys[id] = Decode(value, out int length) is { } bytes && length == EncryptionKeyBytes
                ? bytes
                : throw new InvalidOperationException($"A chave de cifragem '{id}' ({SectionName}:EncryptionKeys:{id}) dos dados pessoais deve ser base64 com exatamente {EncryptionKeyBytes} bytes.");
        }

        return keys.ContainsKey(CurrentKeyId)
            ? keys
            : throw new InvalidOperationException($"{SectionName}:CurrentKeyId aponta para uma chave que não existe em {SectionName}:EncryptionKeys.");
    }

    /// <exception cref="InvalidOperationException">Chave de hash ausente, inválida ou igual a uma chave de cifragem.</exception>
    public byte[] GetHashKey()
    {
        byte[]? bytes = Decode(HashKey, out int length);
        if (bytes is null || length < MinimumHashKeyBytes)
        {
            throw new InvalidOperationException($"{SectionName}:HashKey deve ser base64 com pelo menos {MinimumHashKeyBytes} bytes.");
        }

        if (GetEncryptionKeys().Values.Any(key => key.AsSpan().SequenceEqual(bytes)))
        {
            throw new InvalidOperationException($"{SectionName}:HashKey deve ser diferente de todas as chaves de cifragem.");
        }

        return bytes;
    }

    // Segredos guardados em cofres costumam ganhar uma quebra de linha no fim; TryFromBase64String ignora espaços e quebras.
    private static byte[]? Decode(string value, out int length)
    {
        byte[] buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out length))
        {
            length = 0;
            return null;
        }

        return buffer[..length];
    }

    [GeneratedRegex("^[a-z0-9-]{1,16}$")]
    private static partial Regex KeyIdPattern();
}
