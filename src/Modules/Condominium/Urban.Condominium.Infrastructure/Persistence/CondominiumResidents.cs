using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// Os moradores no PostgreSQL. A leitura só enxerga o condomínio da requisição (filtro de tenant do contexto). Morador é a
/// pessoa com ao menos um vínculo: o síndico sem unidade fica de fora. O CPF repetido é decidido pelo índice único do banco, que
/// vale mesmo com dois cadastros ao mesmo tempo.
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
        db.Set<ChangeLogRecord>().Add(ChangeLogRecord.From(entry));

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(CurrentVersion(person));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CpfIndex })
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(PersonErrors.CpfAlreadyRegistered);
        }
    }

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
