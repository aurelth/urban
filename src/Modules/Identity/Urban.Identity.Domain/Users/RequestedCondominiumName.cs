using Urban.SharedKernel;

namespace Urban.Identity.Domain.Users;

/// <summary>
/// Nome do condomínio que o síndico digitou ao pedir cadastro. É só um texto para o dono reconhecer o pedido e para pré-preencher o
/// formulário depois; as regras do cadastro do condomínio de verdade ficam no módulo Condomínio, que este módulo não conhece.
/// </summary>
public sealed record RequestedCondominiumName
{
    public const int MaxLength = 100;

    private const int MinLength = 2;

    public static readonly Error Invalid = new("identity.requested_condominium_name.invalid", "Informe o nome do condomínio, com 2 a 100 caracteres");

    private RequestedCondominiumName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<RequestedCondominiumName> Create(string? name)
    {
        string raw = name ?? string.Empty;
        string normalized = string.Join(' ', raw.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return !raw.Any(char.IsControl) && IsValid(normalized)
            ? Result.Success(new RequestedCondominiumName(normalized))
            : Result.Failure<RequestedCondominiumName>(Invalid);
    }

    public override string ToString() => Value;

    private static bool IsValid(string name) => name.Length is >= MinLength and <= MaxLength;
}
