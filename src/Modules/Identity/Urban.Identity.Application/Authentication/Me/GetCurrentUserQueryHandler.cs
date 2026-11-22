using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Authentication.Me;

/// <summary>
/// O token só prova quem a pessoa era ao entrar: a conta é lida de novo,
/// para que suspensão ou desativação apareçam na hora e não só quando o token expirar.
/// </summary>
public sealed class GetCurrentUserQueryHandler(IUserAccounts accounts, ICondominiumDirectory directory) : IRequestHandler<GetCurrentUserQuery, Result<CurrentUser>>
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

        // Quem não escolheu "como prefere ser chamado" (o morador) é chamado pelo primeiro nome do cadastro; sem cadastro, o cabeçalho cai no e-mail.
        string? displayName = user.DisplayName?.Value;
        if (displayName is null && user.CondominiumId is not null)
        {
            displayName = await directory.FindFirstNameAsync(user.Id, cancellationToken);
        }

        return Result.Success(new CurrentUser(user.Id, user.Email.Value, user.Role.ToString(), user.CondominiumId, displayName));
    }
}