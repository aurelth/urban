using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Enrollment;

/// <summary>
/// Implementação do contrato que os outros módulos usam para ler e mudar o nome de exibição do síndico. A conta é lida de
/// novo a cada chamada e precisa poder entrar; a regra de quem pode ter nome é do <see cref="User"/>.
/// </summary>
public sealed class ManagerProfile(IUserAccounts accounts) : IManagerProfile
{
    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        User? user = await accounts.FindByIdAsync(userId, cancellationToken);

        return user?.DisplayName?.Value;
    }

    public async Task<Result<string>> UpdateDisplayNameAsync(Guid userId, string? displayName, CancellationToken cancellationToken)
    {
        Result<DisplayName> name = DisplayName.Create(displayName);
        if (name.IsFailure)
        {
            return Result.Failure<string>(name.Error);
        }

        User? user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<string>(SessionErrors.Unauthenticated);
        }

        Result canSignIn = user.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return Result.Failure<string>(canSignIn.Error);
        }

        Result changed = user.ChangeDisplayName(name.Value);
        if (changed.IsFailure)
        {
            return Result.Failure<string>(changed.Error);
        }

        await accounts.SaveEnrollmentAsync(user, cancellationToken);
        return Result.Success(name.Value.Value);
    }
}
