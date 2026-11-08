using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Months;

/// <summary>
/// Fecha o mês. Pede ao Condomínio as unidades ativas com o peso de cada uma (e a falha dele, se os dados ainda não bastam
/// para o método escolhido), deixa o Domain conferir os lançamentos e repartir o total, e grava o fechamento com as cobranças
/// numa unidade atômica. O índice único (condomínio + mês) impede dois fechamentos do mesmo mês ao mesmo tempo.
/// </summary>
public sealed class CloseMonthCommandHandler(
    IExpenseEntries entries,
    IMonthClosings closings,
    IExpenseGroups groups,
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
        Result<MonthClosing> closing = MonthClosing.Close(condominiumId, month, request.DueDate, request.ActorUserId, now, [.. records.Select(record => record.Entry)], basis.Value);

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
}
