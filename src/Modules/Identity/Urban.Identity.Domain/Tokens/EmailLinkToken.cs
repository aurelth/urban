using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>
/// Base dos links enviados por e-mail (confirmação de cadastro e redefinição de senha). Só o hash do segredo é guardado.
/// O link vale uma vez e até o vencimento; cada tipo define a sua validade e os seus erros.
/// </summary>
public abstract class EmailLinkToken
{
    /// <summary>Intervalo mínimo entre dois e-mails para a mesma conta: protege a caixa de quem recebe e a reputação do remetente.</summary>
    public static readonly TimeSpan ResendInterval = TimeSpan.FromMinutes(1);

    private protected EmailLinkToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + lifetime;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string TokenHash { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? UsedAt { get; private set; }

    public bool WasIssuedRecently(DateTimeOffset now) => now < CreatedAt + ResendInterval;

    /// <summary>Marca o link como usado. Link já usado é inválido; link vencido pede um novo.</summary>
    private protected Result ConsumeAt(DateTimeOffset now, Error invalid, Error expired)
    {
        if (UsedAt is not null)
        {
            return Result.Failure(invalid);
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure(expired);
        }

        UsedAt = now;
        return Result.Success();
    }
}