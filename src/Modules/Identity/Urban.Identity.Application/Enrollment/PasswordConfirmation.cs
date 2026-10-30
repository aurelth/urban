using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Enrollment;

/// <summary>
/// Implementação do contrato que confirma a senha de quem já está logado. Usa a mesma conferência do login, então a tentativa
/// errada conta para o bloqueio. A senha errada volta como <c>identity.password.incorrect</c> (e não como erro de login),
/// porque o cliente não deve confundi-la com sessão perdida.
/// </summary>
public sealed class PasswordConfirmation(IUserAccounts accounts) : IPasswordConfirmation
{
    public async Task<Result> ConfirmAsync(Guid userId, string? password, CancellationToken cancellationToken)
    {
        User? user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(SessionErrors.Unauthenticated);
        }

        Result canSignIn = user.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return canSignIn;
        }

        Result<User> checkedUser = await accounts.CheckCredentialsAsync(user.Email, password ?? string.Empty, cancellationToken);

        return checkedUser.IsSuccess
            ? Result.Success()
            : Result.Failure(checkedUser.Error == LoginErrors.LockedOut ? LoginErrors.LockedOut : UserErrors.PasswordIncorrect);
    }
}
