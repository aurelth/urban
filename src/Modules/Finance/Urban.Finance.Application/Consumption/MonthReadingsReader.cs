using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Consumption;

internal static class MonthReadingsReader
{
    public static async Task<Result<MonthReadingsView>> ReadAsync(
        IMeterReadings readings,
        IConsumptionTariffs tariffs,
        IMonthClosings closings,
        IApportionmentBasis apportionment,
        YearMonth month,
        CancellationToken cancellationToken)
    {
        Result<ApportionmentBasis> basis = await apportionment.GetAsync(cancellationToken);

        if (basis.IsFailure)
        {
            return Result.Failure<MonthReadingsView>(basis.Error);
        }

        IReadOnlyList<MeterReading> current = await readings.ListAsync(month, cancellationToken);
        IReadOnlyList<MeterReading> previous = await readings.ListAsync(month.AddMonths(-1), cancellationToken);
        ConsumptionTariff? tariff = ConsumptionTariff.CurrentFor(month, await tariffs.ListAsync(cancellationToken));
        bool isClosed = await closings.FindAsync(month, cancellationToken) is not null;

        IReadOnlyList<UnitReadingsView> units =
        [
            .. basis.Value.Units.Select(unit => new UnitReadingsView(
                unit.UnitId,
                unit.Block,
                unit.Number,
                MeterReadingView.From(Find(current, unit.UnitId, ConsumptionService.Water)),
                MeterReadingView.From(Find(current, unit.UnitId, ConsumptionService.Gas)),
                Find(previous, unit.UnitId, ConsumptionService.Water)?.Current,
                Find(previous, unit.UnitId, ConsumptionService.Gas)?.Current)),
        ];

        return Result.Success(new MonthReadingsView(month.ToString(), isClosed, tariff is not null, tariff?.Gas is not null, units));
    }

    public static MeterReading? Find(IEnumerable<MeterReading> readings, Guid unitId, ConsumptionService service) =>
        readings.FirstOrDefault(reading => reading.UnitId == unitId && reading.Service == service);
}
