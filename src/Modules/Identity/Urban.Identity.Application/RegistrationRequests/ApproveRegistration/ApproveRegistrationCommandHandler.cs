using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.RegistrationRequests.ApproveRegistration;

/// <summary>
/// Aprova o pedido e só então pede o envio do link de confirmação: se a gravação falhar ou perder a corrida para outra decisão,
/// nenhum e-mail sai. Se o pedido do envio falhar depois de gravar, a pessoa pode pedir o cadastro de novo e recebe o link,
/// porque a conta já está aguardando a confirmação.
/// </summary>
public sealed class ApproveRegistrationCommandHandler(
    IUserAccounts accounts,
    IRegistrationRequests requests,
    IEmailVerificationDispatcher dispatcher,
    IClock clock) : IRequestHandler<ApproveRegistrationCommand, Result>
{
    public async Task<Result> Handle(ApproveRegistrationCommand request, CancellationToken cancellationToken)
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

        if (!RegistrationReviewers.CanDecide(reviewer!, ManagementAction.Approve, target))
        {
            return Result.Failure(RegistrationRequestErrors.Forbidden);
        }

        Result approved = target.Approve(reviewer!.Id, clock.UtcNow);
        if (approved.IsFailure)
        {
            return approved;
        }

        if (!await requests.TryDecideAsync(target, AccountStatus.PendingApproval, cancellationToken))
        {
            return Result.Failure(UserErrors.InvalidTransition);
        }

        await dispatcher.EnqueueAsync(target.Id, cancellationToken);

        return Result.Success();
    }
}
