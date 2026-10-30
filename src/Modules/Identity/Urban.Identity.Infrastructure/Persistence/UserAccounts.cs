using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>
/// Confere e-mail e senha e aplica o bloqueio por tentativas. A regra do bloqueio vive em <see cref="UserCredential"/>;
/// aqui ficam a leitura, a gravação e a defesa contra requisições simultâneas.
/// </summary>
public sealed class UserAccounts(IdentityDbContext db, IPasswordHasher passwordHasher, IClock clock) : IUserAccounts
{
    /// <summary>Quantas vezes repetir a conferência quando outra requisição altera a mesma credencial ao mesmo tempo.</summary>
    private const int MaxAttempts = 3;

    public async Task<Result<User>> CheckCredentialsAsync(Email email, string password, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await CheckOnceAsync(email, password, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Outra requisição gravou a credencial no meio da nossa: recomeça com os valores atuais para não perder a contagem.
                db.ChangeTracker.Clear();
            }
        }
    }

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task SaveEnrollmentAsync(User user, CancellationToken cancellationToken)
    {
        // Anexa como "sem mudanças" e marca só as duas colunas que o vínculo altera.
        EntityEntry<User> entry = db.Users.Attach(user);
        entry.Property(u => u.CondominiumId).IsModified = true;
        entry.Property(u => u.DisplayName).IsModified = true;

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<UserCredential?> FindCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
        db.UserCredentials.SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public async Task SaveCredentialAsync(UserCredential credential, CancellationToken cancellationToken)
    {
        if (db.Entry(credential).State == EntityState.Detached)
        {
            db.UserCredentials.Update(credential);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result<User>> CheckOnceAsync(Email email, string password, CancellationToken cancellationToken)
    {
        UserWithCredential? found = await (
            from u in db.Users
            join c in db.UserCredentials on u.Id equals c.UserId
            where u.Email == email
            select new UserWithCredential(u, c)).SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            // Gasta o mesmo tempo de uma conferência real: a demora não revela se o e-mail existe.
            passwordHasher.VerifyDummy(password);
            return Result.Failure<User>(LoginErrors.InvalidCredentials);
        }

        DateTimeOffset now = clock.UtcNow;
        UserCredential credential = found.Credential;
        if (credential.IsLockedOut(now))
        {
            return Result.Failure<User>(LoginErrors.LockedOut);
        }

        if (passwordHasher.Verify(password, credential.PasswordHash))
        {
            credential.RegisterSuccess();
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(found.User);
        }

        credential.RegisterFailure(now);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Failure<User>(credential.IsLockedOut(now) ? LoginErrors.LockedOut : LoginErrors.InvalidCredentials);
    }

    private sealed record UserWithCredential(User User, UserCredential Credential);
}