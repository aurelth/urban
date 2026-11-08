using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Consumption;

/// <summary>
/// Grava as leituras do mês. Primeiro confere o pedido inteiro (unidades do condomínio, serviço, leituras válidas, mês aberto)
/// sem mexer em nada; só então cria, corrige e retira as leituras e grava tudo de uma vez.
/// </summary>
public sealed class SaveMonthReadingsCommandHandler(
    IMeterReadings readings,
    IConsumptionTariffs tariffs,
    IMonthClosings closings,
    IApportionmentBasis apportionment,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<SaveMonthReadingsCommand, Result<MonthReadingsView>>
{
    public async Task<Result<MonthReadingsView>> Handle(SaveMonthReadingsCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<MonthReadingsView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> parsed = YearMonth.Parse(request.Month);

        if (parsed.IsFailure)
        {
            return Result.Failure<MonthReadingsView>(parsed.Error);
        }

        YearMonth month = parsed.Value;

        if (await closings.FindAsync(month, cancellationToken) is not null)
        {
            return Result.Failure<MonthReadingsView>(ClosingErrors.MonthIsClosed);
        }

        Result<ApportionmentBasis> basis = await apportionment.GetAsync(cancellationToken);

        if (basis.IsFailure)
        {
            return Result.Failure<MonthReadingsView>(basis.Error);
        }

        Result saved = await ApplyAsync(condominiumId, month, request.Readings, basis.Value, cancellationToken);

        return saved.IsSuccess
            ? await MonthReadingsReader.ReadAsync(readings, tariffs, closings, apportionment, month, cancellationToken)
            : Result.Failure<MonthReadingsView>(saved.Error);
    }

    private async Task<Result> ApplyAsync(Guid condominiumId, YearMonth month, IReadOnlyList<ReadingInput>? inputs, ApportionmentBasis basis, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReadingInput> requested = LastOfEach(inputs);

        if (requested.Count == 0)
        {
            return Result.Success();
        }

        Result? invalid = FirstInvalid(requested, basis);

        if (invalid is not null)
        {
            return invalid;
        }

        IReadOnlyList<MeterReading> stored = await readings.ListAsync(month, cancellationToken);
        IReadOnlyList<MeterReading> lastMonth = await readings.ListAsync(month.AddMonths(-1), cancellationToken);
        List<MeterReading> added = [];
        List<MeterReading> removed = [];
        List<(MeterReading Reading, decimal Previous, decimal Current)> corrections = [];

        foreach (ReadingInput input in requested)
        {
            MeterReading? existing = MonthReadingsReader.Find(stored, input.UnitId, input.Service);

            if (input.Current is not { } current)
            {
                if (existing is not null)
                {
                    removed.Add(existing);
                }

                continue;
            }

            decimal previous = input.Previous ?? existing?.Previous ?? MonthReadingsReader.Find(lastMonth, input.UnitId, input.Service)?.Current ?? 0m;

            if (!MeterReading.AreValid(previous, current))
            {
                return Result.Failure(ConsumptionErrors.ReadingInvalid);
            }

            if (existing is not null)
            {
                corrections.Add((existing, previous, current));
                continue;
            }

            Result<MeterReading> created = MeterReading.Create(condominiumId, input.UnitId, month, input.Service, previous, current, clock.UtcNow);

            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            added.Add(created.Value);
        }

        foreach ((MeterReading reading, decimal previous, decimal current) in corrections)
        {
            reading.Correct(previous, current);
        }

        return await readings.SaveAsync(added, removed, cancellationToken);
    }

    // Repetida a unidade e o serviço, vale a última informação do pedido.
    private static IReadOnlyList<ReadingInput> LastOfEach(IReadOnlyList<ReadingInput>? inputs) =>
        inputs is null
            ? []
            : [.. inputs.Reverse().DistinctBy(input => (input.UnitId, input.Service)).Reverse()];

    private static Result? FirstInvalid(IReadOnlyList<ReadingInput> requested, ApportionmentBasis basis)
    {
        HashSet<Guid> units = [.. basis.Units.Select(unit => unit.UnitId)];

        if (requested.Any(input => !Enum.IsDefined(input.Service)))
        {
            return Result.Failure(ConsumptionErrors.ServiceInvalid);
        }

        return requested.Any(input => !units.Contains(input.UnitId))
            ? Result.Failure(ConsumptionErrors.UnitNotFound)
            : null;
    }
}
