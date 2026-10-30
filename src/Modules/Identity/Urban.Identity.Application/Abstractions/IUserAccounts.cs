using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Abstractions;

/// <summary>
/// Porta para o armazenamento de contas e senhas.
/// Quem implementa conta as falhas de senha e bloqueia a conta; e-mail inexistente e senha errada devolvem o mesmo erro.
/// </summary>
public interface IUserAccounts
{
    /// <returns>O usuário quando a senha confere; senão falha com <c>LoginErrors.InvalidCredentials</c> ou <c>LoginErrors.LockedOut</c>.</returns>
    Task<Result<User>> CheckCredentialsAsync(Email email, string password, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>
    /// Grava só o condomínio e o nome de exibição de um usuário lido antes. Os outros campos não são reescritos,
    /// para não desfazer, por exemplo, uma suspensão feita no meio do caminho.
    /// </summary>
    Task SaveEnrollmentAsync(User user, CancellationToken cancellationToken);

    /// <summary>Carrega a credencial da conta para ser alterada e gravada por <see cref="SaveCredentialAsync"/>.</summary>
    Task<UserCredential?> FindCredentialAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Grava a credencial (nova senha) lida por <see cref="FindCredentialAsync"/>. Se outra requisição a alterou no meio do caminho, o erro de concorrência sobe.</summary>
    Task SaveCredentialAsync(UserCredential credential, CancellationToken cancellationToken);
}
