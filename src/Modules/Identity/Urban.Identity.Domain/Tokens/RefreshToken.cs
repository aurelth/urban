using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>
/// Token de renovação de sessão. Só o hash é guardado (o valor em texto nunca é persistido). Cada uso gira o token:
/// o atual é revogado e um novo é emitido; reapresentar um token já revogado é sinal de reuso.
/// </summary>
public sealed class RefreshToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt)
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

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken(Guid.CreateVersion7(), userId, tokenHash, now);
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Revoga este token e devolve o substituto. Token já revogado é reuso; token vencido não pode girar.</summary>
    public Result<RefreshToken> Rotate(string newTokenHash, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return Result.Failure<RefreshToken>(RefreshTokenErrors.Reused);
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure<RefreshToken>(RefreshTokenErrors.Expired);
        }

        RefreshToken replacement = Issue(UserId, newTokenHash, now);
        RevokedAt = now;
        ReplacedById = replacement.Id;
        return Result.Success(replacement);
    }

    /// <summary>Revoga o token. Chamar de novo não muda o instante original.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
