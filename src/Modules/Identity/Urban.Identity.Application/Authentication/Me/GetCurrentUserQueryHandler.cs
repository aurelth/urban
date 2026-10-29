using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication.Me;

/// <summary>
/// O token só prova quem a pessoa era ao entrar: a conta é lida de novo,
/// para que suspensão ou desativação apareçam na hora e não só quando o token expirar.
/// </summary>
public sealed class GetCurrentUserQueryHandler(IUserAccounts accounts) : IRequestHandler<GetCurrentUserQuery, Result<CurrentUser>>
{
    public async Task<Result<CurrentUser>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        User? user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<CurrentUser>(SessionErrors.Unauthenticated);
        }

        Result canSignIn = user.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return Result.Failure<CurrentUser>(canSignIn.Error);
        }

        return Result.Success(new CurrentUser(user.Id, user.Email.Value, user.Role.ToString(), user.CondominiumId, user.DisplayName?.Value));
    }
}