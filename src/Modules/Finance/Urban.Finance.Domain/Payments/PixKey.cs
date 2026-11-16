using System.Text.RegularExpressions;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

/// <summary>
/// A chave Pix do condomínio, já no formato em que o banco a reconhece: CNPJ (numérico ou alfanumérico) e CPF só com os
/// caracteres úteis, e-mail em minúsculas, celular com +55 e chave aleatória em minúsculas. A conferência é de formato: os dígitos
/// verificadores do CPF e do CNPJ não são recalculados aqui, porque o banco recusa a chave errada na hora do pagamento.
/// </summary>
public sealed partial record PixKey
{
    public const int MaxLength = 77;

    private PixKey(PixKeyKind kind, string key)
    {
        Kind = kind;
        Key = key;
    }

    public PixKeyKind Kind { get; }

    public string Key { get; }

    public static Result<PixKey> Create(PixKeyKind kind, string? key)
    {
        if (!Enum.IsDefined(kind))
        {
            return Result.Failure<PixKey>(PaymentInstructionsErrors.PixKeyKindInvalid);
        }

        string? normalized = Normalize(kind, key?.Trim());

        return normalized is null
            ? Result.Failure<PixKey>(PaymentInstructionsErrors.PixKeyInvalid)
            : Result.Success(new PixKey(kind, normalized));
    }

    private static string? Normalize(PixKeyKind kind, string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Any(char.IsControl))
        {
            return null;
        }

        return kind switch
        {
            PixKeyKind.Cnpj => Match(StripPunctuation(key).ToUpperInvariant(), CnpjPattern()),
            PixKeyKind.Cpf => Match(StripPunctuation(key), CpfPattern()),
            PixKeyKind.Email => key.Length <= MaxLength ? Match(key.ToLowerInvariant(), EmailPattern()) : null,
            PixKeyKind.Phone => NormalizePhone(key),
            _ => Match(key.ToLowerInvariant(), RandomPattern()),
        };
    }

    private static string StripPunctuation(string key) => key.Replace(".", string.Empty).Replace("/", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

    private static string? Match(string value, Regex pattern) => pattern.IsMatch(value) ? value : null;

    /// <summary>O +55 só se descarta com o sinal de mais, para não confundir com o DDD 55.</summary>
    private static string? NormalizePhone(string key)
    {
        string digits = new(key.Where(char.IsAsciiDigit).ToArray());
        bool onlyPhoneCharacters = key.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '(' or ')' or '-');

        if (!onlyPhoneCharacters || key.LastIndexOf('+') > 0)
        {
            return null;
        }

        if (key.StartsWith("+55", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        return MobilePattern().IsMatch(digits) ? "+55" + digits : null;
    }

    [GeneratedRegex("^[0-9A-Z]{12}[0-9]{2}$")]
    private static partial Regex CnpjPattern();

    [GeneratedRegex("^[0-9]{11}$")]
    private static partial Regex CpfPattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[1-9][1-9]9[0-9]{8}$")]
    private static partial Regex MobilePattern();

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex RandomPattern();
}
