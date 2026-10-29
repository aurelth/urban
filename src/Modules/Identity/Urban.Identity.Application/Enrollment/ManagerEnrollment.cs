using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Enrollment;

/// <summary>
/// Implementação do contrato que os outros módulos usam para vincular o síndico ao condomínio. A conta é lida de novo
/// (o token só diz quem a pessoa era ao entrar), precisa poder entrar, e a regra de quem pode se vincular é do <see cref="User"/>.
/// </summary>
public sealed class ManagerEnrollment(IUserAccounts accounts) : IManagerEnrollment
{
    public async Task<Result> EnrollAsync(Guid userId, Guid condominiumId, string? displayName, CancellationToken cancellationToken)
    {
        Result<DisplayName> name = DisplayName.Create(displayName);
        if (name.IsFailure)
        {
            return Result.Failure(name.Error);
        }

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

        Result assigned = user.AssignCondominium(condominiumId, name.Value);
        if (assigned.IsFailure)
        {
            return assigned;
        }

        await accounts.SaveEnrollmentAsync(user, cancellationToken);
        return Result.Success();
    }
}
