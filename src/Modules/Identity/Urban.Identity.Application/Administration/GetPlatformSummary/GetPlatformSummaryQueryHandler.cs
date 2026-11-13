using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Administration.GetPlatformSummary;

public sealed class GetPlatformSummaryQueryHandler(IUserAccounts accounts, IManagerAccounts managers)
    : IRequestHandler<GetPlatformSummaryQuery, Result<PlatformSummary>>
{
    public async Task<Result<PlatformSummary>> Handle(GetPlatformSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!RegistrationReviewers.IsReviewer(await accounts.FindByIdAsync(request.ActorId, cancellationToken)))
        {
            return Result.Failure<PlatformSummary>(AdministrationErrors.Forbidden);
        }

        return Result.Success(await managers.SummarizeAsync(cancellationToken));
    }
}
