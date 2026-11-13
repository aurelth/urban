using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

/// <summary>E-mail de contato de uma pessoa, normalizado (sem espaços nas pontas e em minúsculas). Só existe se for válido.</summary>
public sealed record ContactEmail
{
    public const int MaxLength = 254;

    public static readonly Error Invalid = new("condominium.email.invalid", "Informe um e-mail válido");

    private ContactEmail(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<ContactEmail> Create(string? address)
    {
        string normalized = (address ?? string.Empty).Trim().ToLowerInvariant();

        return IsValid(normalized)
            ? Result.Success(new ContactEmail(normalized))
            : Result.Failure<ContactEmail>(Invalid);
    }

    /// <summary>Reconstitui um e-mail já aceito no cadastro, vindo do armazenamento.</summary>
    public static ContactEmail Restore(string value) => new(value);

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
