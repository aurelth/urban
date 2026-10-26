using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>
/// Link de redefinição de senha: vale 1 hora, bem menos que o de confirmação, porque abre a porta de uma conta que já existe.
/// </summary>
public sealed class PasswordResetToken : EmailLinkToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private PasswordResetToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt)
        : base(id, userId, tokenHash, createdAt, Lifetime)
    {
    }

    public static PasswordResetToken Issue(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, tokenHash, now);

    public Result Consume(DateTimeOffset now) =>
        ConsumeAt(now, PasswordResetTokenErrors.Invalid, PasswordResetTokenErrors.Expired);
}