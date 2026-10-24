using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>
/// Persistência dos tokens de renovação. Vive no escopo da requisição (um <see cref="IdentityDbContext"/> por vez), então
/// o token carregado por <see cref="FindByHashAsync"/> já está rastreado e basta salvar. Um token carregado por outro
/// contexto também é aceito: nesse caso ele é anexado como alterado.
/// </summary>
public sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        TrackAsModified(token);
        return db.SaveChangesAsync(cancellationToken);
    }

    public Task ReplaceAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken)
    {
        TrackAsModified(current);
        db.RefreshTokens.Add(replacement);
        return db.SaveChangesAsync(cancellationToken);
    }

    public Task RevokeAllOfUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private void TrackAsModified(RefreshToken token)
    {
        if (db.Entry(token).State == EntityState.Detached)
        {
            db.RefreshTokens.Update(token);
        }
    }
}
