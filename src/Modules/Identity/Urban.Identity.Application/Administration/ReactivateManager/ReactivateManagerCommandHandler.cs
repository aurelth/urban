using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Administration.ReactivateManager;

/// <summary>Tira a suspensão e apaga o motivo. A pessoa entra de novo com a mesma senha: as sessões antigas continuam encerradas.</summary>
public sealed class ReactivateManagerCommandHandler(
    IUserAccounts accounts,
    IManagerAccounts managers,
    IClock clock) : IRequestHandler<ReactivateManagerCommand, Result>
{
    public async Task<Result> Handle(ReactivateManagerCommand request, CancellationToken cancellationToken)
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

        if (!RegistrationReviewers.CanDecide(administrator!, ManagementAction.Reactivate, target))
        {
            return Result.Failure(AdministrationErrors.Forbidden);
        }

        AccountStatus current = target.Status;
        Result reactivated = target.Reactivate(administrator!.Id, clock.UtcNow);
        if (reactivated.IsFailure)
        {
            return reactivated;
        }

        return await managers.TryChangeStatusAsync(target, current, cancellationToken)
            ? Result.Success()
            : Result.Failure(UserErrors.InvalidTransition);
    }
}
