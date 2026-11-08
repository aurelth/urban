using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>
/// As despesas no PostgreSQL, sempre dentro do condomínio da requisição. Parcelas e reajustes pertencem à despesa (carregam
/// e gravam junto). Como mexer só neles não muda a linha da despesa, cada gravação incrementa a revisão da despesa: a versão
/// (xmin) passa a valer para a despesa inteira.
/// </summary>
public sealed class FinanceExpenses(FinanceDbContext db) : IExpenses
{
    private const string VersionProperty = "xmin";

    public async Task<IReadOnlyList<ExpenseRecord>> ListAsync(CancellationToken cancellationToken)
    {
        List<Expense> expenses = await db.Expenses.ToListAsync(cancellationToken);

        return [.. expenses.OrderBy(expense => expense.Description, TextOrder.Comparer).Select(expense => new ExpenseRecord(expense, CurrentVersion(expense)))];
    }

    public async Task<ExpenseRecord?> FindAsync(Guid expenseId, CancellationToken cancellationToken)
    {
        Expense? expense = await db.Expenses.SingleOrDefaultAsync(item => item.Id == expenseId, cancellationToken);

        return expense is null ? null : new ExpenseRecord(expense, CurrentVersion(expense));
    }

    public async Task<Result<string>> AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        db.Expenses.Add(expense);

        return await SaveChangesAsync(expense, cancellationToken);
    }

    public async Task<Result<string>> SaveAsync(ExpenseRecord record, CancellationToken cancellationToken)
    {
        PropertyEntry<Expense, int> revision = db.Entry(record.Expense).Property<int>(ExpenseConfiguration.RevisionProperty);
        revision.CurrentValue += 1;

        return await SaveChangesAsync(record.Expense, cancellationToken);
    }

    private async Task<Result<string>> SaveChangesAsync(Expense expense, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(CurrentVersion(expense));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(FinanceErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsAnyUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(FinanceErrors.ConcurrencyConflict);
        }
    }

    private string CurrentVersion(Expense expense) =>
        db.Entry(expense).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);
}
