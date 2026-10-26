using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>
/// Persistência da redefinição de senha. A redefinição é um único <c>SaveChanges</c>: o link usado, a nova senha e o uso dos
/// outros links abertos da conta gravam juntos ou não gravam. Conflitos (link usado ou credencial alterada por outra
/// requisição) viram <c>false</c>; qualquer outra falha sobe.
/// </summary>
public sealed class PasswordResets(IdentityDbContext db) : IPasswordResets
{
    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken)
    {
        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.PasswordResetTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<PasswordResetToken?> FindLatestOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.PasswordResetTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UserCredential?> FindCredentialOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.UserCredentials.SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public async Task<bool> ResetAsync(PasswordResetToken token, UserCredential credential, CancellationToken cancellationToken)
    {
        if (token.UsedAt is not DateTimeOffset usedAt)
        {
            throw new InvalidOperationException("O link precisa ser marcado como usado antes de gravar a redefinição.");
        }

        if (db.Entry(token).State == EntityState.Detached)
        {
            db.PasswordResetTokens.Update(token);
        }

        if (db.Entry(credential).State == EntityState.Detached)
        {
            db.UserCredentials.Update(credential);
        }

        List<PasswordResetToken> others = await db.PasswordResetTokens
            .Where(t => t.UserId == token.UserId && t.UsedAt == null && t.Id != token.Id)
            .ToListAsync(cancellationToken);
        foreach (PasswordResetToken other in others)
        {
            _ = other.Consume(usedAt);
        }

        return await db.TrySaveAsync(cancellationToken);
    }
}