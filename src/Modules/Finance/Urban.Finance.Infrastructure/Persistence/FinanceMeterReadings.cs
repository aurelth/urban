using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>As leituras dos medidores no PostgreSQL, sempre dentro do condomínio da requisição.</summary>
public sealed class FinanceMeterReadings(FinanceDbContext db) : IMeterReadings
{
    public async Task<IReadOnlyList<MeterReading>> ListAsync(YearMonth month, CancellationToken cancellationToken) =>
        await db.MeterReadings.Where(reading => reading.Month == month).ToListAsync(cancellationToken);

    public async Task<Result> SaveAsync(IReadOnlyList<MeterReading> added, IReadOnlyList<MeterReading> removed, CancellationToken cancellationToken)
    {
        db.MeterReadings.AddRange(added);
        db.MeterReadings.RemoveRange(removed);

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, MeterReadingConfiguration.UnitMonthServiceIndex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure(FinanceErrors.ConcurrencyConflict);
        }
    }
}
