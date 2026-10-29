using Urban.SharedKernel;

namespace Urban.Identity.Domain.Users;

/// <summary>Como a pessoa prefere ser chamada ("Olá Ana!"). Fica na conta, e não no cadastro do condomínio, porque o cabeçalho a lê em toda sessão.</summary>
public sealed record DisplayName
{
    public const int MaxLength = 40;

    private const int MinLetters = 2;

    public static readonly Error Invalid = new("identity.display_name.invalid", "Informe como prefere ser chamado, com 2 a 40 letras");

    private DisplayName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<DisplayName> Create(string? name)
    {
        string normalized = string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return IsValid(normalized)
            ? Result.Success(new DisplayName(normalized))
            : Result.Failure<DisplayName>(Invalid);
    }

    public override string ToString() => Value;

    private static bool IsValid(string name) =>
        name.Length <= MaxLength
        && name.All(c => char.IsLetter(c) || c is ' ' or '\'' or '-' or '.')
        && name.Count(char.IsLetter) >= MinLetters;
}
