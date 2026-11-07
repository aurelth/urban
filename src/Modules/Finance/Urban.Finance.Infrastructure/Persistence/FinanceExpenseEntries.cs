using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>Os lançamentos mensais no PostgreSQL, sempre dentro do condomínio da requisição.</summary>
public sealed class FinanceExpenseEntries(FinanceDbContext db) : IExpenseEntries
{
    private const string VersionProperty = "xmin";

    public async Task<IReadOnlyList<ExpenseEntryRecord>> ListAsync(YearMonth month, CancellationToken cancellationToken)
    {
        List<ExpenseEntry> entries = await db.ExpenseEntries.Where(entry => entry.Month == month).ToListAsync(cancellationToken);

        return [.. entries.Select(Record)];
    }

    public async Task<IReadOnlyList<ExpenseEntryRecord>> ListForExpenseFromAsync(Guid expenseId, YearMonth from, CancellationToken cancellationToken)
    {
        // O recorte por mês é em memória: a competência é um texto no banco e o operador >= do tipo não vira SQL. Uma despesa tem poucas dezenas de lançamentos.
        List<ExpenseEntry> entries = await db.ExpenseEntries.Where(entry => entry.ExpenseId == expenseId).ToListAsync(cancellationToken);

        return [.. entries.Where(entry => entry.Month >= from).OrderBy(entry => entry.Month).Select(Record)];
    }

    public async Task<ExpenseEntryRecord?> FindAsync(Guid entryId, CancellationToken cancellationToken)
    {
        ExpenseEntry? entry = await db.ExpenseEntries.SingleOrDefaultAsync(item => item.Id == entryId, cancellationToken);

        return entry is null ? null : Record(entry);
    }

    public async Task<Result> AddRangeAsync(IReadOnlyList<ExpenseEntry> entries, CancellationToken cancellationToken)
    {
        db.ExpenseEntries.AddRange(entries);

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, ExpenseEntryConfiguration.ExpenseMonthIndex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure(FinanceErrors.ConcurrencyConflict);
        }
    }

    public async Task<Result<string>> SaveAsync(ExpenseEntryRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(CurrentVersion(record.Entry));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(FinanceErrors.ConcurrencyConflict);
        }
    }

    private ExpenseEntryRecord Record(ExpenseEntry entry) => new(entry, CurrentVersion(entry));

    private string CurrentVersion(ExpenseEntry entry) =>
        db.Entry(entry).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);
}
