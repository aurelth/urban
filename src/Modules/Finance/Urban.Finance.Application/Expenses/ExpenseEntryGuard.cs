using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Expenses;

internal static class ExpenseEntryGuard
{
    /// <summary>
    /// Confere se os lançamentos que vão sumir (encerrar ou excluir a despesa) podem sumir: nenhum num mês já fechado, porque a cobrança
    /// emitida os usa, e nenhum já pago. O mês fechado vale primeiro, porque a saída dele (reabrir) vem antes de desfazer pagamentos.
    /// </summary>
    public static async Task<Result> CheckRemovableAsync(
        IMonthClosings closings,
        IReadOnlyList<ExpenseEntryRecord> doomed,
        Error closedMonths,
        Error paidEntries,
        CancellationToken cancellationToken)
    {
        if (doomed.Count == 0)
        {
            return Result.Success();
        }

        YearMonth first = doomed.Min(record => record.Entry.Month);
        IReadOnlySet<YearMonth> closed = await closings.ClosedMonthsFromAsync(first, cancellationToken);

        if (doomed.Any(record => closed.Contains(record.Entry.Month)))
        {
            return Result.Failure(closedMonths);
        }

        return doomed.Any(record => record.Entry.Status == EntryStatus.Paid)
            ? Result.Failure(paidEntries)
            : Result.Success();
    }
}
