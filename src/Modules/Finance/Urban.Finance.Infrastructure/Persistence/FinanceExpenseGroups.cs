using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>Os grupos de despesa no PostgreSQL, sempre dentro do condomínio da requisição (filtro de tenant do contexto).</summary>
public sealed class FinanceExpenseGroups(FinanceDbContext db) : IExpenseGroups
{
    private const string VersionProperty = "xmin";

    public async Task<IReadOnlyList<ExpenseGroupRecord>> ListAsync(CancellationToken cancellationToken)
    {
        List<ExpenseGroup> groups = await db.ExpenseGroups.ToListAsync(cancellationToken);

        return [.. groups.OrderBy(group => group.Name, TextOrder.Comparer).Select(group => new ExpenseGroupRecord(group, CurrentVersion(group)))];
    }

    public async Task<ExpenseGroupRecord?> FindAsync(Guid groupId, CancellationToken cancellationToken)
    {
        ExpenseGroup? group = await db.ExpenseGroups.SingleOrDefaultAsync(item => item.Id == groupId, cancellationToken);

        return group is null ? null : new ExpenseGroupRecord(group, CurrentVersion(group));
    }

    public async Task<bool> IsNameTakenAsync(string name, Guid? exceptGroupId, CancellationToken cancellationToken)
    {
        string lowered = name.ToLowerInvariant();

        return await db.ExpenseGroups.AnyAsync(
            group => (exceptGroupId == null || group.Id != exceptGroupId) && group.Name.ToLower() == lowered,
            cancellationToken);
    }

    public async Task<Result<string>> AddAsync(ExpenseGroup group, CancellationToken cancellationToken)
    {
        db.ExpenseGroups.Add(group);

        return await SaveAsync(group, cancellationToken);
    }

    public async Task<Result<string>> SaveAsync(ExpenseGroupRecord record, CancellationToken cancellationToken) =>
        await SaveAsync(record.Group, cancellationToken);

    private async Task<Result<string>> SaveAsync(ExpenseGroup group, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(CurrentVersion(group));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(FinanceErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, ExpenseGroupConfiguration.NameIndex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(ExpenseGroupErrors.NameAlreadyExists);
        }
    }

    private string CurrentVersion(ExpenseGroup group) =>
        db.Entry(group).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);
}
