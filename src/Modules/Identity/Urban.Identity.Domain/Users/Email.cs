using Urban.SharedKernel;

namespace Urban.Identity.Domain.Users;

/// <summary>Endereço de e-mail normalizado (sem espaços nas pontas e em minúsculas). Só existe se for válido.</summary>
public sealed record Email
{
    public const int MaxLength = 254;

    public static readonly Error Invalid = new("identity.email.invalid", "Informe um e-mail válido");

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Email> Create(string? address)
    {
        string normalized = (address ?? string.Empty).Trim().ToLowerInvariant();

        return IsValid(normalized)
            ? Result.Success(new Email(normalized))
            : Result.Failure<Email>(Invalid);
    }

    public override string ToString() => Value;

    private static bool IsValid(string value)
    {
        int at = value.IndexOf('@');
        if (value.Length > MaxLength || at <= 0 || at != value.LastIndexOf('@') || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        string domain = value[(at + 1)..];
        return domain.Contains('.')
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal);
    }
}
