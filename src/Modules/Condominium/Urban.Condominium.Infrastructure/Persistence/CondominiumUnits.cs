using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// As unidades no PostgreSQL. A leitura só enxerga o condomínio da requisição (filtro de tenant do contexto). Toda gravação
/// segue o mesmo caminho: abre uma transação (se quem chamou ainda não abriu), trava a linha do condomínio, confere de novo
/// o identificador e a soma das frações, grava e confirma. Travar a linha faz dois cadastros simultâneos do mesmo condomínio
/// passarem um de cada vez, então a conferência vale mesmo com concorrência. O índice único do banco é a segunda barreira.
/// </summary>
public sealed class CondominiumUnits(CondominiumDbContext db) : IUnits
{
    private const string VersionProperty = "xmin";

    public async Task<UnitRecord?> FindAsync(Guid unitId, CancellationToken cancellationToken)
    {
        CondominiumUnit? unit = await db.Units.SingleOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        return unit is null ? null : new UnitRecord(unit, CurrentVersion(unit));
    }

    public async Task<PagedUnits> ListAsync(UnitFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<CondominiumUnit> query = Filtered(db.Units.AsNoTracking(), filter);

        int total = await query.CountAsync(cancellationToken);
        List<UnitRow> rows = await query
            .OrderBy(u => u.Block != null)
            .ThenBy(u => EF.Functions.Collate(u.Block!, CondominiumPersistence.NaturalCollation))
            .ThenBy(u => EF.Functions.Collate(u.Number, CondominiumPersistence.NaturalCollation))
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UnitRow(u, EF.Property<uint>(u, VersionProperty)))
            .ToListAsync(cancellationToken);

        return new PagedUnits([.. rows.Select(r => new UnitRecord(r.Unit, r.Version.ToString(CultureInfo.InvariantCulture)))], total);
    }

    public async Task<IReadOnlyList<CondominiumUnit>> AllAsync(CancellationToken cancellationToken)
    {
        List<CondominiumUnit> all = await db.Units.AsNoTracking().ToListAsync(cancellationToken);
        all.Sort(UnitOrder.Comparer);

        return all;
    }

    public async Task<decimal> ActiveFractionTotalAsync(Guid? exceptUnitId, CancellationToken cancellationToken) =>
        await db.Units
            .Where(u => u.DeactivatedAt == null && u.IdealFraction != null && (exceptUnitId == null || u.Id != exceptUnitId))
            .SumAsync(u => u.IdealFraction!.Value, cancellationToken);

    public async Task<bool> IsIdentityTakenAsync(UnitIdentity identity, Guid? exceptUnitId, CancellationToken cancellationToken)
    {
        string block = identity.Block.ToLowerInvariant();
        string number = identity.Number.ToLowerInvariant();

        // Mesma expressão do índice único: bloco vazio é "sem bloco", e as duas partes sem diferenciar maiúsculas.
        return await db.Units.AnyAsync(
            u => (exceptUnitId == null || u.Id != exceptUnitId) && (u.Block ?? string.Empty).ToLower() == block && u.Number.ToLower() == number,
            cancellationToken);
    }

    public async Task<IReadOnlySet<UnitIdentity>> TakenAmongAsync(IReadOnlyCollection<UnitIdentity> identities, CancellationToken cancellationToken)
    {
        // Um condomínio tem de dezenas a poucas centenas de unidades: ler só bloco e número e comparar em memória custa menos que montar uma consulta com 500 pares.
        List<StoredIdentity> existing = await db.Units.AsNoTracking().Select(u => new StoredIdentity(u.Block, u.Number)).ToListAsync(cancellationToken);
        HashSet<UnitIdentity> wanted = [.. identities];

        return existing.Select(u => new UnitIdentity(u.Block, u.Number)).Where(wanted.Contains).ToHashSet();
    }

    public async Task<Result<string>> AddAsync(CondominiumUnit unit, ChangeLogEntry entry, CancellationToken cancellationToken) =>
        await WriteAsync(
            unit.CondominiumId,
            async () =>
            {
                Result checks = await CheckRulesAsync(unit, cancellationToken);
                if (checks.IsFailure)
                {
                    return Result.Failure<string>(checks.Error);
                }

                db.Units.Add(unit);

                return await SaveChangesAsync(entry, () => CurrentVersion(unit), cancellationToken);
            },
            cancellationToken);

    public async Task<Result<string>> SaveAsync(UnitRecord record, ChangeLogEntry entry, CancellationToken cancellationToken) =>
        await WriteAsync(
            record.Unit.CondominiumId,
            async () =>
            {
                Result checks = await CheckRulesAsync(record.Unit, cancellationToken);

                return checks.IsFailure
                    ? Result.Failure<string>(checks.Error)
                    : await SaveChangesAsync(entry, () => CurrentVersion(record.Unit), cancellationToken);
            },
            cancellationToken);

    public async Task<Result> AddRangeAsync(IReadOnlyList<CondominiumUnit> units, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        Result<string> written = await WriteAsync(
            entry.CondominiumId,
            async () =>
            {
                IReadOnlySet<UnitIdentity> taken = await TakenAmongAsync([.. units.Select(u => u.Identity)], cancellationToken);
                if (taken.Count > 0)
                {
                    return Result.Failure<string>(UnitErrors.IdentifierAlreadyExists);
                }

                Result fits = IdealFractions.EnsureWithinWhole(await ActiveFractionTotalAsync(null, cancellationToken) + IdealFractions.TotalOf(units));
                if (fits.IsFailure)
                {
                    return Result.Failure<string>(fits.Error);
                }

                db.Units.AddRange(units);

                return await SaveChangesAsync(entry, () => string.Empty, cancellationToken);
            },
            cancellationToken);

        return written.IsSuccess ? Result.Success() : Result.Failure(written.Error);
    }

    private static IQueryable<CondominiumUnit> Filtered(IQueryable<CondominiumUnit> units, UnitFilter filter)
    {
        IQueryable<CondominiumUnit> query = filter.Status switch
        {
            UnitStatusFilter.Active => units.Where(u => u.DeactivatedAt == null),
            UnitStatusFilter.Deactivated => units.Where(u => u.DeactivatedAt != null),
            _ => units,
        };

        if (filter.Block is { } block)
        {
            string lowered = block.ToLowerInvariant();
            query = query.Where(u => u.Block != null && u.Block.ToLower() == lowered);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(u => u.Type == type);
        }

        if (filter.Search is { } search)
        {
            string pattern = $"%{EscapeLike(search)}%";
            query = query.Where(u => EF.Functions.ILike(u.Number, pattern, "\\") || (u.Block != null && EF.Functions.ILike(u.Block, pattern, "\\")));
        }

        return query;
    }

    /// <summary>Faz % e _ valerem como caracteres comuns na busca (ILIKE os trata como curingas).</summary>
    private static string EscapeLike(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>As duas regras que dependem das outras unidades, conferidas com a linha do condomínio travada.</summary>
    private async Task<Result> CheckRulesAsync(CondominiumUnit unit, CancellationToken cancellationToken)
    {
        if (await IsIdentityTakenAsync(unit.Identity, unit.Id, cancellationToken))
        {
            return Result.Failure(UnitErrors.IdentifierAlreadyExists);
        }

        if (!unit.IsActive || unit.IdealFraction is not { } fraction)
        {
            return Result.Success();
        }

        return IdealFractions.EnsureWithinWhole(await ActiveFractionTotalAsync(unit.Id, cancellationToken) + fraction);
    }

    /// <summary>Abre uma transação própria quando ninguém abriu, trava a linha do condomínio e só confirma se o trabalho deu certo.</summary>
    private async Task<Result<string>> WriteAsync(Guid condominiumId, Func<Task<Result<string>>> work, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        // NO KEY UPDATE trava outras gravações de unidades do mesmo condomínio sem atrapalhar quem só referencia a linha (chave estrangeira).
        await db.Database.ExecuteSqlInterpolatedAsync($"select 1 from condominium.condominium where id = {condominiumId} for no key update", cancellationToken);

        Result<string> result = await work();
        if (result.IsSuccess && own is not null)
        {
            await own.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<Result<string>> SaveChangesAsync(ChangeLogEntry entry, Func<string> version, CancellationToken cancellationToken)
    {
        db.Set<ChangeLogRecord>().Add(ChangeLogRecord.From(entry));

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(version());
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(CondominiumErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: UnitConfiguration.IdentifierIndex })
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(UnitErrors.IdentifierAlreadyExists);
        }
    }

    private string CurrentVersion(CondominiumUnit unit) =>
        db.Entry(unit).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);

    private sealed record UnitRow(CondominiumUnit Unit, uint Version);

    private sealed record StoredIdentity(string? Block, string Number);
}
