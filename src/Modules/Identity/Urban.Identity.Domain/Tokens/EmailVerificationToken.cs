using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>
/// Link de confirmação de e-mail. Só o hash do segredo é guardado. Vale uma vez e por 24 horas; quem prova que lê a
/// caixa de e-mail é quem escolhe a senha, então ninguém consegue "reservar" a conta de outra pessoa.
/// </summary>
public sealed class EmailVerificationToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>Intervalo mínimo entre dois e-mails para a mesma conta: protege a caixa de quem recebe e a reputação do remetente.</summary>
    public static readonly TimeSpan ResendInterval = TimeSpan.FromMinutes(1);

    private EmailVerificationToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + Lifetime;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string TokenHash { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static EmailVerificationToken Issue(Guid userId, string tokenHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new EmailVerificationToken(Guid.CreateVersion7(), userId, tokenHash, now);
    }

    public bool WasIssuedRecently(DateTimeOffset now) => now < CreatedAt + ResendInterval;

    /// <summary>Marca o link como usado. Link já usado é inválido; link vencido pede um novo cadastro.</summary>
    public Result Consume(DateTimeOffset now)
    {
        if (UsedAt is not null)
        {
            return Result.Failure(EmailVerificationTokenErrors.Invalid);
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure(EmailVerificationTokenErrors.Expired);
        }

        UsedAt = now;
        return Result.Success();
    }
}