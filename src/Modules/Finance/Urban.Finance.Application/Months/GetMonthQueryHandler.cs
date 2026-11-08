using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

public sealed class GetMonthQueryHandler(IExpenseEntries entries, IExpenseGroups groups, ITenantContext tenant, IClock clock)
    : IRequestHandler<GetMonthQuery, Result<MonthView>>
{
    public async Task<Result<MonthView>> Handle(GetMonthQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MonthView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        return month.IsFailure
            ? Result.Failure<MonthView>(month.Error)
            : Result.Success(await MonthReader.ReadAsync(entries, groups, month.Value, FinanceCalendar.Today(clock.UtcNow), cancellationToken));
    }
}
