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
}
