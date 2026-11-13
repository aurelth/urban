using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Administration.SuspendManager;

/// <summary>
/// Suspende o síndico com motivo. A gravação só vale se a conta ainda estiver no estado em que foi lida, então duas decisões
/// simultâneas não se atropelam. Gravada a suspensão, todas as sessões da pessoa terminam na hora (o token de acesso dura
/// minutos, e sem isso ela continuaria entrando até ele vencer).
/// </summary>
public sealed class SuspendManagerCommandHandler(
    IUserAccounts accounts,
    IManagerAccounts managers,
    IRefreshTokenRepository refreshTokens,
    ISessionRevocations sessions,
    IClock clock) : IRequestHandler<SuspendManagerCommand, Result>
{
    public async Task<Result> Handle(SuspendManagerCommand request, CancellationToken cancellationToken)
    {
        User? administrator = await accounts.FindByIdAsync(request.ActorId, cancellationToken);
        if (!RegistrationReviewers.IsReviewer(administrator))
        {
            return Result.Failure(AdministrationErrors.Forbidden);
        }

        User? target = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (target is not { Role: UserRole.Manager })
        {
            return Result.Failure(AdministrationErrors.ManagerNotFound);
        }

        if (!RegistrationReviewers.CanDecide(administrator!, ManagementAction.Suspend, target))
        {
            return Result.Failure(AdministrationErrors.Forbidden);
        }

        AccountStatus current = target.Status;
        DateTimeOffset now = clock.UtcNow;
        Result suspended = target.Suspend(administrator!.Id, request.Reason, now);
        if (suspended.IsFailure)
        {
            return suspended;
        }

        if (!await managers.TryChangeStatusAsync(target, current, cancellationToken))
        {
            return Result.Failure(UserErrors.InvalidTransition);
        }

        await refreshTokens.RevokeAllOfUserAsync(target.Id, now, cancellationToken);
        await sessions.RevokeAllOfUserAsync(target.Id, now, cancellationToken);

        return Result.Success();
    }
}
