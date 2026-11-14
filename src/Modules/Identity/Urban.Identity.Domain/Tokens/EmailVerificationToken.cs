using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>
/// Link de confirmação de e-mail: vale 24 horas. Quem prova que lê a caixa de e-mail é quem escolhe a senha, então
/// ninguém consegue "reservar" a conta de outra pessoa. O convite de um morador usa o mesmo link, mas vale 72 horas: ele não
/// está esperando o e-mail na hora, como quem acabou de se cadastrar.
/// </summary>
public sealed class EmailVerificationToken : EmailLinkToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(72);

    // O EF Core recria o link por este construtor (o vencimento vem do banco); a validade só importa na emissão.
    private EmailVerificationToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt)
        : base(id, userId, tokenHash, createdAt, Lifetime)
    {
    }

    private EmailVerificationToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt, TimeSpan lifetime)
        : base(id, userId, tokenHash, createdAt, lifetime)
    {
    }

    public static EmailVerificationToken Issue(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, tokenHash, now);

    public static EmailVerificationToken IssueInvitation(Guid userId, string tokenHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, tokenHash, now, InvitationLifetime);

    public Result Consume(DateTimeOffset now) =>
        ConsumeAt(now, EmailVerificationTokenErrors.Invalid, EmailVerificationTokenErrors.Expired);
}