using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>Os fechamentos de mês e as cobranças no PostgreSQL, sempre dentro do condomínio da requisição.</summary>
public sealed class FinanceMonthClosings(FinanceDbContext db) : IMonthClosings
{
    public async Task<MonthClosing?> FindAsync(YearMonth month, CancellationToken cancellationToken) =>
        await db.MonthClosings
            .Include(closing => closing.Charges.OrderBy(charge => charge.Position))
            .ThenInclude(charge => charge.Lines.OrderBy(line => line.Position))
            .SingleOrDefaultAsync(closing => closing.Month == month, cancellationToken);

    public async Task<IReadOnlySet<YearMonth>> ClosedMonthsFromAsync(YearMonth from, CancellationToken cancellationToken)
    {
        // O recorte por mês é em memória, como nos lançamentos: a competência é um texto no banco. São poucos meses por condomínio.
        List<YearMonth> months = await db.MonthClosings.Select(closing => closing.Month).ToListAsync(cancellationToken);

        return months.Where(month => month >= from).ToHashSet();
    }

    public async Task<Result> AddAsync(MonthClosing closing, CancellationToken cancellationToken)
    {
        db.MonthClosings.Add(closing);

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, MonthClosingConfiguration.CondominiumMonthIndex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure(ClosingErrors.AlreadyClosed);
        }
    }

    public async Task RemoveAsync(MonthClosing closing, CancellationToken cancellationToken)
    {
        db.MonthClosings.Remove(closing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
