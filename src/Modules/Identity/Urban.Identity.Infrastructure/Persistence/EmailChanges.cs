using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>
/// Persistência da troca de e-mail. Pedir um link novo dá por usados os anteriores da conta (vale o último pedido). A
/// confirmação é um único <c>SaveChanges</c>: o link usado, o novo e-mail e o uso dos outros links abertos gravam juntos
/// ou não gravam. Conflitos (link usado por outra requisição, conta alterada no meio do caminho ou endereço que virou de
/// outra conta) viram <c>false</c>; qualquer outra falha sobe.
/// </summary>
public sealed class EmailChanges(IdentityDbContext db) : IEmailChanges
{
    public async Task AddAsync(EmailChangeToken token, CancellationToken cancellationToken)
    {
        List<EmailChangeToken> open = await db.EmailChangeTokens
            .Where(t => t.UserId == token.UserId && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (EmailChangeToken previous in open)
        {
            _ = previous.Consume(token.CreatedAt);
        }

        db.EmailChangeTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<EmailChangeToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.EmailChangeTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<EmailChangeToken?> FindLatestOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.EmailChangeTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ConfirmAsync(EmailChangeToken token, User user, CancellationToken cancellationToken)
    {
        if (token.UsedAt is not DateTimeOffset usedAt)
        {
            throw new InvalidOperationException("O link precisa ser marcado como usado antes de gravar a troca de e-mail.");
        }

        if (db.Entry(token).State == EntityState.Detached)
        {
            db.EmailChangeTokens.Update(token);
        }

        if (db.Entry(user).State == EntityState.Detached)
        {
            db.Users.Attach(user).Property(u => u.Email).IsModified = true;
        }

        List<EmailChangeToken> others = await db.EmailChangeTokens
            .Where(t => t.UserId == token.UserId && t.UsedAt == null && t.Id != token.Id)
            .ToListAsync(cancellationToken);
        foreach (EmailChangeToken other in others)
        {
            _ = other.Consume(usedAt);
        }

        return await db.TrySaveAsync(cancellationToken);
    }
}
