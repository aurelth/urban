using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.RegistrationRequests.RejectRegistration;

/// <summary>A recusa é final e silenciosa: nada é enviado à pessoa, e novos pedidos com o mesmo e-mail continuam sendo ignorados.</summary>
public sealed class RejectRegistrationCommandHandler(
    IUserAccounts accounts,
    IRegistrationRequests requests,
    IClock clock) : IRequestHandler<RejectRegistrationCommand, Result>
{
    public async Task<Result> Handle(RejectRegistrationCommand request, CancellationToken cancellationToken)
    {
        User? reviewer = await accounts.FindByIdAsync(request.ActorId, cancellationToken);
        if (!RegistrationReviewers.IsReviewer(reviewer))
        {
            return Result.Failure(RegistrationRequestErrors.Forbidden);
        }

        User? target = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (target is null)
        {
            return Result.Failure(RegistrationRequestErrors.NotFound);
        }

        if (!RegistrationReviewers.CanDecide(reviewer!, ManagementAction.Reject, target))
        {
            return Result.Failure(RegistrationRequestErrors.Forbidden);
        }

        Result rejected = target.Reject(reviewer!.Id, clock.UtcNow);
        if (rejected.IsFailure)
        {
            return rejected;
        }

        return await requests.TryDecideAsync(target, AccountStatus.PendingApproval, cancellationToken)
            ? Result.Success()
            : Result.Failure(UserErrors.InvalidTransition);
    }
}
