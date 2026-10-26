using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>
/// Persistência do cadastro e da confirmação de e-mail. Cada operação é um único <c>SaveChanges</c>, então grava tudo ou
/// nada. Conflitos (e-mail repetido, link usado por outra requisição) viram <c>false</c>; qualquer outra falha sobe.
/// </summary>
public sealed class EmailVerifications(IdentityDbContext db) : IEmailVerifications
{
    public async Task<bool> TryRegisterAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);

        return await db.TrySaveAsync(cancellationToken);
    }

    public async Task AddAsync(EmailVerificationToken token, CancellationToken cancellationToken)
    {
        db.EmailVerificationTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<EmailVerificationToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<EmailVerificationToken?> FindLatestOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ConfirmAsync(
        EmailVerificationToken token,
        User user,
        UserCredential credential,
        CancellationToken cancellationToken)
    {
        if (db.Entry(token).State == EntityState.Detached)
        {
            db.EmailVerificationTokens.Update(token);
        }

        if (db.Entry(user).State == EntityState.Detached)
        {
            db.Users.Update(user);
        }

        db.UserCredentials.Add(credential);

        return await db.TrySaveAsync(cancellationToken);
    }
}