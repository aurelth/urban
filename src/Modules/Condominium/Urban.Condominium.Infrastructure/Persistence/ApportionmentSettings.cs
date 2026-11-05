using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.SharedKernel;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// O método de rateio mora na linha do condomínio. A versão é o <c>xmin</c> dela: o PostgreSQL o troca a cada UPDATE, então
/// uma troca feita a partir de uma leitura velha encontra outro valor e é recusada. O registro da alteração vai no mesmo
/// <c>SaveChanges</c>.
/// </summary>
public sealed class ApportionmentSettings(CondominiumDbContext db) : IApportionments
{
    private const string VersionProperty = "xmin";

    public async Task<ApportionmentSetting?> FindAsync(Guid condominiumId, CancellationToken cancellationToken)
    {
        CondominiumEntity? condominium = await db.Condominiums.SingleOrDefaultAsync(c => c.Id == condominiumId, cancellationToken);

        return condominium is null ? null : new ApportionmentSetting(condominium, CurrentVersion(condominium));
    }

    public async Task<Result<string>> SaveAsync(ApportionmentSetting setting, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        db.Set<ChangeLogRecord>().Add(ChangeLogRecord.From(entry));

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(CurrentVersion(setting.Condominium));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(CondominiumErrors.ConcurrencyConflict);
        }
    }

    private string CurrentVersion(CondominiumEntity condominium) =>
        db.Entry(condominium).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);
}
