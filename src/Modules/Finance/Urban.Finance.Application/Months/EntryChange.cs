using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

/// <summary>O que toda mudança em um lançamento tem em comum: condomínio, versão, achar, mudar, gravar e devolver o lançamento com a versão nova.</summary>
internal static class EntryChange
{
    public static async Task<Result<ExpenseEntryView>> ApplyAsync(
        IExpenseEntries entries,
        ITenantContext tenant,
        IClock clock,
        Guid entryId,
        string? version,
        Func<ExpenseEntryRecord, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ExpenseEntryView>(FinanceErrors.CondominiumNotFound);
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure<ExpenseEntryView>(FinanceErrors.VersionRequired);
        }

        ExpenseEntryRecord? record = await entries.FindAsync(entryId, cancellationToken);

        if (record is null)
        {
            return Result.Failure<ExpenseEntryView>(ExpenseErrors.EntryNotFound);
        }

        if (version != record.Version)
        {
            return Result.Failure<ExpenseEntryView>(FinanceErrors.ConcurrencyConflict);
        }

        Result changed = await change(record);

        if (changed.IsFailure)
        {
            return Result.Failure<ExpenseEntryView>(changed.Error);
        }

        Result<string> saved = await entries.SaveAsync(record, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ExpenseEntryView.From(record with { Version = saved.Value }, null, FinanceCalendar.Today(clock.UtcNow)))
            : Result.Failure<ExpenseEntryView>(saved.Error);
    }

    /// <summary>Mês fechado não aceita mudança no lançamento: a cobrança emitida e a fatura já usam o que está nele.</summary>
    public static async Task<Result> WhileMonthIsOpenAsync(IMonthClosings closings, ExpenseEntry entry, Func<ExpenseEntry, Result> change, CancellationToken cancellationToken) =>
        await closings.FindAsync(entry.Month, cancellationToken) is not null
            ? Result.Failure(ClosingErrors.MonthIsClosed)
            : change(entry);
}
