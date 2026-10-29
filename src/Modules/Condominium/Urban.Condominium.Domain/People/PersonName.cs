using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

/// <summary>Nome completo: ao menos nome e sobrenome, só letras, espaço, apóstrofo, hífen e ponto (para iniciais).</summary>
public sealed record PersonName
{
    private const int MaxLength = 120;
    private const int MinWordLength = 2;

    public static readonly Error Invalid = new("condominium.person.full_name_invalid", "Informe o nome e o sobrenome");

    private PersonName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<PersonName> Create(string? fullName)
    {
        string normalized = TextRules.Normalize(fullName);

        return IsValid(normalized)
            ? Result.Success(new PersonName(normalized))
            : Result.Failure<PersonName>(Invalid);
    }

    public override string ToString() => Value;

    private static bool IsValid(string name)
    {
        if (name.Length == 0 || name.Length > MaxLength || !name.All(IsAllowed))
        {
            return false;
        }

        string[] words = name.Split(' ');

        return words.Length >= 2
            && words[0].Length >= MinWordLength
            && words[^1].Length >= MinWordLength;
    }

    private static bool IsAllowed(char c) => char.IsLetter(c) || c is ' ' or '\'' or '-' or '.';
}
