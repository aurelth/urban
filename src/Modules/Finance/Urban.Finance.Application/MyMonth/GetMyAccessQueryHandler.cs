using MediatR;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.MyMonth;

public sealed class GetMyAccessQueryHandler(IResidentHousing housing, ITenantContext tenant, IClock clock)
    : IRequestHandler<GetMyAccessQuery, Result<MyMonthAccess>>
{
    public async Task<Result<MyMonthAccess>> Handle(GetMyAccessQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MyMonthAccess>(FinanceErrors.CondominiumNotFound);
        }

        ResidentDwelling dwelling = await housing.GetAsync(request.UserId, FinanceCalendar.Today(clock.UtcNow), cancellationToken);

        return Result.Success(MyAccess.Of(dwelling));
    }
}
