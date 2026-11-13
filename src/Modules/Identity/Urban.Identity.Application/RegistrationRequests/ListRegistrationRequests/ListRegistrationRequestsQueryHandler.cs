using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.SharedKernel;

namespace Urban.Identity.Application.RegistrationRequests.ListRegistrationRequests;

public sealed class ListRegistrationRequestsQueryHandler(
    IUserAccounts accounts,
    IRegistrationRequests requests) : IRequestHandler<ListRegistrationRequestsQuery, Result<IReadOnlyList<RegistrationRequestSummary>>>
{
    public async Task<Result<IReadOnlyList<RegistrationRequestSummary>>> Handle(
        ListRegistrationRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (!RegistrationReviewers.IsReviewer(await accounts.FindByIdAsync(request.ActorId, cancellationToken)))
        {
            return Result.Failure<IReadOnlyList<RegistrationRequestSummary>>(RegistrationRequestErrors.Forbidden);
        }

        return Result.Success(await requests.ListPendingAsync(cancellationToken));
    }
}
