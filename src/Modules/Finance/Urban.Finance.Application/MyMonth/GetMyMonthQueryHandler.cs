using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.MyMonth;

public sealed class GetMyMonthQueryHandler : IRequestHandler<GetMyMonthQuery, Result<MyMonthView>>
{
    public GetMyMonthQueryHandler(
        IExpenseEntries entries, IMonthClosings closings, IExpenseGroups groups, IPayments payments, IResidentHousing housing, ITenantContext tenant, IClock clock)
    {
        _ = (entries, closings, groups, payments, housing, tenant, clock);
    }

    public Task<Result<MyMonthView>> Handle(GetMyMonthQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<MyMonthView>(FinanceErrors.CondominiumNotFound));
}
