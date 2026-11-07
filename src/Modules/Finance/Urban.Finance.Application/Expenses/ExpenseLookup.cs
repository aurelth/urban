using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Expenses;

internal static class ExpenseLookup
{
    /// <summary>Acha a despesa que a tela editou e confere a versão que ela leu.</summary>
    public static async Task<Result<ExpenseRecord>> FindForChangeAsync(IExpenses expenses, Guid expenseId, string? version, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure<ExpenseRecord>(FinanceErrors.VersionRequired);
        }

        ExpenseRecord? record = await expenses.FindAsync(expenseId, cancellationToken);

        if (record is null)
        {
            return Result.Failure<ExpenseRecord>(ExpenseErrors.NotFound);
        }

        return version == record.Version
            ? Result.Success(record)
            : Result.Failure<ExpenseRecord>(FinanceErrors.ConcurrencyConflict);
    }

    /// <summary>
    /// Depois de mudar o valor de uma despesa, os lançamentos que já existem e que ninguém tinha editado à mão acompanham o novo
    /// valor. O lançamento editado à mão (valor diferente do que a despesa previa) fica como está.
    /// </summary>
    public static async Task<Result> FollowNewAmountsAsync(
        IExpenseEntries entries,
        Expense expense,
        IReadOnlyList<ExpenseEntryRecord> records,
        IReadOnlyDictionary<Guid, decimal?> amountsBefore,
        CancellationToken cancellationToken)
    {
        foreach (ExpenseEntryRecord record in records)
        {
            decimal? before = amountsBefore[record.Entry.Id];
            decimal? after = expense.AmountFor(record.Entry.Month);

            if (before is not { } oldAmount || after is not { } newAmount || oldAmount == newAmount || record.Entry.Amount != oldAmount)
            {
                continue;
            }

            record.Entry.SetAmount(newAmount);
            Result<string> saved = await entries.SaveAsync(record, cancellationToken);

            if (saved.IsFailure)
            {
                return Result.Failure(saved.Error);
            }
        }

        return Result.Success();
    }

    public static Dictionary<Guid, decimal?> AmountsOf(Expense expense, IReadOnlyList<ExpenseEntryRecord> records) =>
        records.ToDictionary(record => record.Entry.Id, record => expense.AmountFor(record.Entry.Month));
}
