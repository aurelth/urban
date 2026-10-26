using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta de persistência da redefinição de senha. Cada operação grava por conta própria e de forma atômica.</summary>
public interface IPasswordResets
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken);

    Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<PasswordResetToken?> FindLatestOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Carrega a credencial para ser alterada e gravada por <see cref="ResetAsync"/>.</summary>
    Task<UserCredential?> FindCredentialOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Grava, na mesma transação, o link já usado, a nova senha da credencial e o uso dos outros links ainda abertos da conta.
    /// O link e a credencial precisam ter vindo de <see cref="FindByHashAsync"/> e <see cref="FindCredentialOfUserAsync"/>.
    /// </summary>
    /// <returns><c>false</c> quando outra requisição usou o link ou alterou a credencial antes; nada é gravado.</returns>
    Task<bool> ResetAsync(PasswordResetToken token, UserCredential credential, CancellationToken cancellationToken);
}