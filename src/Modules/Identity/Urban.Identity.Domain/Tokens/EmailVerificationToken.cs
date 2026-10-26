using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>
/// Link de confirmação de e-mail: vale 24 horas. Quem prova que lê a caixa de e-mail é quem escolhe a senha, então
/// ninguém consegue "reservar" a conta de outra pessoa.
/// </summary>
public sealed class EmailVerificationToken : EmailLinkToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private EmailVerificationToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt)
        : base(id, userId, tokenHash, createdAt, Lifetime)
    {
    }

    public static EmailVerificationToken Issue(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, tokenHash, now);

    public Result Consume(DateTimeOffset now) =>
        ConsumeAt(now, EmailVerificationTokenErrors.Invalid, EmailVerificationTokenErrors.Expired);
}