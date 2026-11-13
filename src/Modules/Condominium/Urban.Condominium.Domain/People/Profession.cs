using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

/// <summary>Profissão de um morador adulto: texto livre de 2 a 80 caracteres, sem caracteres de controle.</summary>
public sealed record Profession
{
    public const int MinLength = 2;
    public const int MaxLength = 80;

    public static readonly Error Invalid = new("condominium.profession.invalid", "Informe a profissão, com 2 a 80 caracteres");

    private Profession(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Profession> Create(string? text)
    {
        string normalized = TextRules.Normalize(text);

        return normalized.Length is >= MinLength and <= MaxLength && !TextRules.HasControlCharacters(normalized)
            ? Result.Success(new Profession(normalized))
            : Result.Failure<Profession>(Invalid);
    }

    public override string ToString() => Value;
}
