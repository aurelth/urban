using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// Os moradores no PostgreSQL. A leitura só enxerga o condomínio da requisição (filtro de tenant do contexto). Morador é a
/// pessoa com ao menos um vínculo: o síndico sem unidade fica de fora. O CPF repetido e o segundo vínculo em andamento na mesma
/// unidade são decididos pelos índices únicos do banco, que valem mesmo com dois pedidos ao mesmo tempo; a versão velha de uma
/// edição é decidida pelo <c>xmin</c>.
/// </summary>
public sealed class CondominiumResidents(CondominiumDbContext db) : IResidents
{
    private const string CpfIndex = "ix_person_condominium_id_cpf_hash";
    private const string VersionProperty = "xmin";

    public async Task<ResidentRecord?> FindAsync(Guid personId, CancellationToken cancellationToken)
    {
        Person? person = await db.People.SingleOrDefaultAsync(p => p.Id == personId && db.Occupancies.Any(o => o.PersonId == p.Id), cancellationToken);
        if (person is null)
        {
            return null;
        }

        ILookup<Guid, ResidentOccupancy> occupancies = await OccupanciesOfAsync([personId], cancellationToken);

        return new ResidentRecord(person, [.. occupancies[personId]], CurrentVersion(person));
    }

    public async Task<PagedResidents> ListAsync(ResidentFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Person> people = db.People.AsNoTracking();
        if (filter.Search is { } search)
        {
            // O nome tem conversor de valor, então a busca por trecho vai em SQL: só os ids que casam, como subconsulta, e o filtro de
            // tenant continua valendo na consulta de fora. O padrão chega como parâmetro, nunca concatenado.
            string pattern = $"%{EscapeLike(search)}%";
            IQueryable<Guid> matching = db.Database.SqlQuery<Guid>($"""select id as "Value" from condominium.person where full_name ilike {pattern} escape '\'""");
            people = people.Where(p => matching.Contains(p.Id));
        }

        Guid? unitId = filter.UnitId;
        IQueryable<Person> query = people.Where(p => db.Occupancies.Any(o => o.PersonId == p.Id && (unitId == null || o.UnitId == unitId)));

        int total = await query.CountAsync(cancellationToken);
        List<PersonRow> rows = await query
            .OrderBy(p => p.FullName)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PersonRow(p, EF.Property<uint>(p, VersionProperty)))
            .ToListAsync(cancellationToken);

        ILookup<Guid, ResidentOccupancy> occupancies = await OccupanciesOfAsync([.. rows.Select(r => r.Person.Id)], cancellationToken);

        return new PagedResidents(
            [.. rows.Select(r => new ResidentRecord(r.Person, [.. occupancies[r.Person.Id]], r.Version.ToString(CultureInfo.InvariantCulture)))],
            total);
    }

    public async Task<Result<string>> AddAsync(Person person, Occupancy occupancy, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        db.People.Add(person);
        db.Occupancies.Add(occupancy);

        return await SaveWithLogAsync(entry, () => CurrentVersion(person), cancellationToken);
    }

    public async Task<Result<string>> SaveAsync(ResidentRecord record, ChangeLogEntry entry, CancellationToken cancellationToken) =>
        await SaveWithLogAsync(entry, () => CurrentVersion(record.Person), cancellationToken);

    public async Task<Result> AddOccupancyAsync(Occupancy occupancy, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        db.Occupancies.Add(occupancy);

        Result<string> saved = await SaveWithLogAsync(entry, () => string.Empty, cancellationToken);

        return saved.IsSuccess ? Result.Success() : Result.Failure(saved.Error);
    }

    /// <summary>
    /// A leitura dos vínculos não rastreia as entidades, então não há versão para comparar. Em vez disso a atualização é
    /// condicional ("só se ainda não tem data de saída"): de dois pedidos ao mesmo tempo, só o primeiro muda uma linha. O registro
    /// vai na mesma transação, para não ficar nem sem ele nem com ele sem a saída.
    /// </summary>
    public async Task<Result> EndOccupancyAsync(Occupancy occupancy, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        DateOnly? endDate = occupancy.EndDate;
        int ended = await db.Occupancies
            .Where(o => o.Id == occupancy.Id && o.EndDate == null)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.EndDate, endDate), cancellationToken);
        if (ended == 0)
        {
            return Result.Failure(OccupancyErrors.AlreadyEnded);
        }

        db.Set<ChangeLogRecord>().Add(ChangeLogRecord.From(entry));
        await db.SaveChangesAsync(cancellationToken);

        if (own is not null)
        {
            await own.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>Grava o que já está no contexto junto com o registro de alterações e traduz as violações que o banco decide.</summary>
    private async Task<Result<string>> SaveWithLogAsync(ChangeLogEntry entry, Func<string> version, CancellationToken cancellationToken)
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
        catch (DbUpdateException ex) when (Violates(ex, CpfIndex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(PersonErrors.CpfAlreadyRegistered);
        }
        catch (DbUpdateException ex) when (Violates(ex, OccupancyConfiguration.ActivePerPersonAndUnitIndex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(OccupancyErrors.AlreadyActive);
        }
    }

    private static bool Violates(DbUpdateException ex, string index) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation && violation.ConstraintName == index;

    /// <summary>Todos os vínculos das pessoas dadas, com bloco e número da unidade, os em andamento primeiro e depois os mais recentes.</summary>
    private async Task<ILookup<Guid, ResidentOccupancy>> OccupanciesOfAsync(IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        List<OccupancyRow> rows = await (
            from occupancy in db.Occupancies.AsNoTracking()
            join unit in db.Units.AsNoTracking() on occupancy.UnitId equals unit.Id
            where personIds.Contains(occupancy.PersonId)
            select new OccupancyRow(occupancy, unit.Block, unit.Number)).ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => !r.Occupancy.IsActive)
            .ThenByDescending(r => r.Occupancy.StartDate)
            .ThenBy(r => r.Occupancy.Id)
            .ToLookup(r => r.Occupancy.PersonId, r => new ResidentOccupancy(r.Occupancy, r.UnitBlock, r.UnitNumber));
    }

    /// <summary>Faz % e _ valerem como caracteres comuns na busca (ILIKE os trata como curingas).</summary>
    private static string EscapeLike(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private string CurrentVersion(Person person) =>
        db.Entry(person).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);

    private sealed record PersonRow(Person Person, uint Version);

    private sealed record OccupancyRow(Occupancy Occupancy, string? UnitBlock, string UnitNumber);
}
