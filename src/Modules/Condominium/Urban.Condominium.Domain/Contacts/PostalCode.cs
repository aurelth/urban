using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Contacts;

public sealed record PostalCode
{
    private const int Length = 8;

    public static readonly Error Invalid = new("condominium.postal_code.invalid", "Informe um CEP válido");

    private PostalCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string Formatted => $"{Value[..5]}-{Value[5..]}";

    public static Result<PostalCode> Create(string? code)
    {
        string digits = TextRules.RemoveAny(code ?? string.Empty, ".-");

        return digits.Length == Length && digits.All(TextRules.IsAsciiDigit) && digits.Any(c => c != '0')
            ? Result.Success(new PostalCode(digits))
            : Result.Failure<PostalCode>(Invalid);
    }

    public override string ToString() => Value;
}
