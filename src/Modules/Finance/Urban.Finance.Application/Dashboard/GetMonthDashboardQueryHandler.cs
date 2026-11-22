using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Dashboard;

public sealed class GetMonthDashboardQueryHandler : IRequestHandler<GetMonthDashboardQuery, Result<MonthDashboardView>>
{
    public GetMonthDashboardQueryHandler(
        IExpenseEntries entries, IMonthClosings closings, IExpenseGroups groups, IPayments payments, ITenantContext tenant, IClock clock)
    {
    }

    public Task<Result<MonthDashboardView>> Handle(GetMonthDashboardQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<MonthDashboardView>(FinanceErrors.CondominiumNotFound));
}
