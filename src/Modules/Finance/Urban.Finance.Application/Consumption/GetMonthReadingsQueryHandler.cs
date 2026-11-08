using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Consumption;

public sealed class GetMonthReadingsQueryHandler(
    IMeterReadings readings,
    IConsumptionTariffs tariffs,
    IMonthClosings closings,
    IApportionmentBasis apportionment,
    ITenantContext tenant) : IRequestHandler<GetMonthReadingsQuery, Result<MonthReadingsView>>
{
    public async Task<Result<MonthReadingsView>> Handle(GetMonthReadingsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MonthReadingsView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        return month.IsFailure
            ? Result.Failure<MonthReadingsView>(month.Error)
            : await MonthReadingsReader.ReadAsync(readings, tariffs, closings, apportionment, month.Value, cancellationToken);
    }
}
