using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>As tarifas de consumo no PostgreSQL, sempre dentro do condomínio da requisição.</summary>
public sealed class FinanceConsumptionTariffs(FinanceDbContext db) : IConsumptionTariffs
{
    public async Task<IReadOnlyList<ConsumptionTariff>> ListAsync(CancellationToken cancellationToken)
    {
        // A competência é um texto no banco; a ordem por mês é feita em memória, como nos outros repositórios. São poucas tarifas por condomínio.
        List<ConsumptionTariff> tariffs = await db.ConsumptionTariffs.ToListAsync(cancellationToken);

        return [.. tariffs.OrderByDescending(tariff => tariff.ValidFrom)];
    }

    public async Task<ConsumptionTariff?> FindAsync(YearMonth validFrom, CancellationToken cancellationToken) =>
        await db.ConsumptionTariffs.SingleOrDefaultAsync(tariff => tariff.ValidFrom == validFrom, cancellationToken);

    public async Task<Result> SaveAsync(ConsumptionTariff tariff, CancellationToken cancellationToken)
    {
        ConsumptionTariff? previous = await FindAsync(tariff.ValidFrom, cancellationToken);

        if (previous is not null)
        {
            db.ConsumptionTariffs.Remove(previous);
        }

        db.ConsumptionTariffs.Add(tariff);

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, ConsumptionTariffConfiguration.CondominiumValidFromIndex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure(FinanceErrors.ConcurrencyConflict);
        }
    }

    public async Task RemoveAsync(ConsumptionTariff tariff, CancellationToken cancellationToken)
    {
        db.ConsumptionTariffs.Remove(tariff);
        await db.SaveChangesAsync(cancellationToken);
    }
}
