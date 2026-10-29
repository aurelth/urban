using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Documents;

public sealed record Cpf
{
    private const int Length = 11;

    public static readonly Error Invalid = new("condominium.cpf.invalid", "Informe um CPF válido");

    private Cpf(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string Masked => $"***.{Value[3..6]}.{Value[6..9]}-**";

    public static Result<Cpf> Create(string? number)
    {
        string digits = TextRules.RemoveAny(number ?? string.Empty, ".-");

        return IsValid(digits)
            ? Result.Success(new Cpf(digits))
            : Result.Failure<Cpf>(Invalid);
    }

    // Um CPF é dado pessoal: qualquer log ou mensagem que imprima o objeto sai mascarado.
    public override string ToString() => Masked;

    private static bool IsValid(string digits)
    {
        if (digits.Length != Length || !digits.All(TextRules.IsAsciiDigit) || digits.Distinct().Count() == 1)
        {
            return false;
        }

        return CheckDigit(digits[..9], 10) == digits[9] - '0'
            && CheckDigit(digits[..10], 11) == digits[10] - '0';
    }

    private static int CheckDigit(string digits, int firstWeight)
    {
        int sum = digits.Select((digit, index) => (digit - '0') * (firstWeight - index)).Sum();
        int remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }
}
