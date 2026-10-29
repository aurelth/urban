using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Documents;

/// <summary>CNPJ numérico ou alfanumérico (a Receita emite os novos com letras desde julho de 2026).</summary>
public sealed record Cnpj
{
    private const int Length = 14;

    private static readonly int[] FirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private static readonly int[] SecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static readonly Error Invalid = new("condominium.cnpj.invalid", "Informe um CNPJ válido");

    private Cnpj(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string Formatted => $"{Value[..2]}.{Value[2..5]}.{Value[5..8]}/{Value[8..12]}-{Value[12..]}";

    public static Result<Cnpj> Create(string? number)
    {
        string normalized = ToUpperAscii(TextRules.RemoveAny(number ?? string.Empty, "./-"));

        return IsValid(normalized)
            ? Result.Success(new Cnpj(normalized))
            : Result.Failure<Cnpj>(Invalid);
    }

    public override string ToString() => Value;

    private static string ToUpperAscii(string value) =>
        string.Concat(value.Select(c => c is >= 'a' and <= 'z' ? (char)(c - 32) : c));

    private static bool IsValid(string value)
    {
        if (value.Length != Length
            || !value.All(c => TextRules.IsAsciiDigit(c) || c is >= 'A' and <= 'Z')
            || value.Distinct().Count() == 1)
        {
            return false;
        }

        return CheckDigit(value[..12], FirstWeights) == value[12] - '0'
            && CheckDigit(value[..13], SecondWeights) == value[13] - '0';
    }

    // Os dois últimos caracteres precisam ser os dígitos verificadores: uma letra ali vale 17 ou mais e nunca
    // coincide com o dígito calculado (0 a 9), então não há checagem separada para isso.
    // O valor de cada caractere é o código ASCII menos 48: os algarismos valem 0 a 9 e as letras, 17 a 42.
    private static int CheckDigit(string characters, int[] weights)
    {
        int sum = characters.Select((c, index) => (c - '0') * weights[index]).Sum();
        int remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }
}
