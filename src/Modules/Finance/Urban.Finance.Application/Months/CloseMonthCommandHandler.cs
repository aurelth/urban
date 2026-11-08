using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Settings;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Months;

/// <summary>
/// Fecha o mês. Pede ao Condomínio as unidades ativas com o peso de cada uma (e a falha dele, se os dados ainda não bastam
/// para o método escolhido), deixa o Domain conferir os lançamentos e repartir o total, e grava o fechamento com as cobranças
/// numa unidade atômica. Cada grupo de despesa vira uma linha do recibo; o fundo de reserva, a água, o esgoto e o gás entram conforme
/// a configuração do condomínio e as leituras do mês (sem leitura de uma unidade, o fechamento é recusado). O índice único (condomínio + mês) impede dois fechamentos do mesmo mês ao mesmo tempo.
/// </summary>
public sealed class CloseMonthCommandHandler(
    IExpenseEntries entries,
    IMonthClosings closings,
    IExpenseGroups groups,
    IFinanceSettings settings,
    IConsumptionTariffs tariffs,
    IMeterReadings readings,
    IApportionmentBasis apportionment,
    ITenantContext tenant,
    IAtomicWork atomicWork,
    IClock clock) : IRequestHandler<CloseMonthCommand, Result<MonthView>>
{
    public async Task<Result<MonthView>> Handle(CloseMonthCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<MonthView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> parsed = YearMonth.Parse(request.Month);

        if (parsed.IsFailure)
        {
            return Result.Failure<MonthView>(parsed.Error);
        }

        YearMonth month = parsed.Value;

        if (await closings.FindAsync(month, cancellationToken) is not null)
        {
            return Result.Failure<MonthView>(ClosingErrors.AlreadyClosed);
        }

        IReadOnlyList<ExpenseEntryRecord> records = await entries.ListAsync(month, cancellationToken);
        Result<ApportionmentBasis> basis = await apportionment.GetAsync(cancellationToken);

        if (basis.IsFailure)
        {
            return Result.Failure<MonthView>(basis.Error);
        }

        DateTimeOffset now = clock.UtcNow;
        ClosingOptions options = await OptionsAsync(month, cancellationToken);
        Result<MonthClosing> closing = MonthClosing.Close(condominiumId, month, request.DueDate, request.ActorUserId, now, [.. records.Select(record => record.Entry)], basis.Value, options);

        if (closing.IsFailure)
        {
            return Result.Failure<MonthView>(closing.Error);
        }

        return await Atomic.RunAsync(
            atomicWork,
            async ct =>
            {
                Result added = await closings.AddAsync(closing.Value, ct);

                return added.IsSuccess
                    ? Result.Success(await MonthReader.ReadAsync(entries, closings, groups, month, FinanceCalendar.Today(now), ct))
                    : Result.Failure<MonthView>(added.Error);
            },
            cancellationToken);
    }

    private async Task<ClosingOptions> OptionsAsync(YearMonth month, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseGroupRecord> allGroups = await groups.ListAsync(cancellationToken);
        FinanceSettings? chosen = await settings.FindAsync(cancellationToken);
        ConsumptionTariff? tariff = ConsumptionTariff.CurrentFor(month, await tariffs.ListAsync(cancellationToken));

        return new ClosingOptions(
            allGroups.ToDictionary(record => record.Group.Id, record => record.Group.Name),
            allGroups.Where(record => record.Group.ReserveFundBase).Select(record => record.Group.Id).ToHashSet(),
            chosen?.ReserveFundPercent ?? 0m,
            tariff,
            await readings.ListAsync(month, cancellationToken));
    }
}
