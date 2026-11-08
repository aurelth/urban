using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Settings;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>As escolhas do condomínio no PostgreSQL: uma linha por condomínio, sempre a do tenant da requisição.</summary>
public sealed class FinanceSettingsStore(FinanceDbContext db) : IFinanceSettings
{
    public async Task<FinanceSettings?> FindAsync(CancellationToken cancellationToken) =>
        await db.FinanceSettings.SingleOrDefaultAsync(cancellationToken);

    public async Task<Result> SaveAsync(FinanceSettings settings, CancellationToken cancellationToken)
    {
        if (db.Entry(settings).State == EntityState.Detached)
        {
            db.FinanceSettings.Add(settings);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, FinanceSettingsConfiguration.PrimaryKey))
        {
            db.ChangeTracker.Clear();

            return Result.Failure(FinanceErrors.ConcurrencyConflict);
        }
    }
}
