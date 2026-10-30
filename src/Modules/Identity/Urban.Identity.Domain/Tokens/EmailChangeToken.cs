using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>
/// Link de troca de e-mail: vai para o endereço NOVO e vale 24 horas. Só quem lê aquela caixa consegue confirmar, então
/// ninguém passa a conta para um endereço que não é seu. Guarda o endereço pedido para que o link não sirva para outro.
/// </summary>
public sealed class EmailChangeToken : EmailLinkToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private EmailChangeToken(Guid id, Guid userId, Email newEmail, string tokenHash, DateTimeOffset createdAt)
        : base(id, userId, tokenHash, createdAt, Lifetime)
    {
        NewEmail = newEmail;
    }

    public Email NewEmail { get; }

    public static EmailChangeToken Issue(Guid userId, Email newEmail, string tokenHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, newEmail, tokenHash, now);

    public Result Consume(DateTimeOffset now) =>
        ConsumeAt(now, EmailChangeTokenErrors.Invalid, EmailChangeTokenErrors.Expired);
}
