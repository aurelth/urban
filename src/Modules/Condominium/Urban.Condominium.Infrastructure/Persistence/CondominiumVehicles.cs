using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Vehicles;
using Urban.Condominium.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// Os veículos no PostgreSQL. A leitura só enxerga o condomínio da requisição (filtro de tenant do contexto), e a unidade vem
/// junto na mesma consulta (sem N+1). Cada gravação leva o registro de alterações na mesma <c>SaveChanges</c>. A placa única
/// é do índice do banco: a conferência em memória só dá a resposta rápida, e o índice vira conflito, nunca erro 500.
/// </summary>
public sealed class CondominiumVehicles(CondominiumDbContext db) : IVehicles
{
    private const string VersionProperty = "xmin";

    public async Task<VehicleRecord?> FindAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        // Lida com rastreamento: é o veículo que SaveAsync e DeleteAsync vão gravar. O bloco e o número da unidade vêm na mesma consulta.
        VehicleRow? row = await (
            from vehicle in db.Vehicles
            join unit in db.Units on vehicle.UnitId equals unit.Id
            where vehicle.Id == vehicleId
            select new VehicleRow(vehicle, unit.Block, unit.Number, EF.Property<uint>(vehicle, VersionProperty)))
            .SingleOrDefaultAsync(cancellationToken);

        return row?.ToRecord();
    }

    public async Task<PagedVehicles> ListAsync(VehicleFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        // O filtro só olha o veículo, então a contagem não precisa da unidade; a página junta a unidade e ordena por ela (sem N+1).
        IQueryable<Vehicle> filtered = Filtered(db.Vehicles.AsNoTracking(), filter);

        int total = await filtered.CountAsync(cancellationToken);
        List<VehicleRow> rows = await (
            from vehicle in filtered
            join unit in db.Units on vehicle.UnitId equals unit.Id
            orderby unit.Block != null,
                EF.Functions.Collate(unit.Block!, CondominiumPersistence.NaturalCollation),
                EF.Functions.Collate(unit.Number, CondominiumPersistence.NaturalCollation),
                vehicle.Plate,
                vehicle.Id
            select new VehicleRow(vehicle, unit.Block, unit.Number, EF.Property<uint>(vehicle, VersionProperty)))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedVehicles([.. rows.Select(r => r.ToRecord())], total);
    }

    public async Task<bool> IsPlateTakenAsync(string plate, Guid? exceptVehicleId, CancellationToken cancellationToken) =>
        await db.Vehicles.AnyAsync(v => v.Plate == plate && (exceptVehicleId == null || v.Id != exceptVehicleId), cancellationToken);

    public async Task<Result<string>> AddAsync(Vehicle vehicle, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        db.Vehicles.Add(vehicle);

        return await SaveChangesAsync(entry, () => CurrentVersion(vehicle), cancellationToken);
    }

    public async Task<Result<string>> SaveAsync(VehicleRecord record, ChangeLogEntry entry, CancellationToken cancellationToken) =>
        await SaveChangesAsync(entry, () => CurrentVersion(record.Vehicle), cancellationToken);

    public async Task<Result> DeleteAsync(VehicleRecord record, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        db.Vehicles.Remove(record.Vehicle);
        Result<string> deleted = await SaveChangesAsync(entry, () => string.Empty, cancellationToken);

        return deleted.IsSuccess ? Result.Success() : Result.Failure(deleted.Error);
    }

    private static IQueryable<Vehicle> Filtered(IQueryable<Vehicle> vehicles, VehicleFilter filter)
    {
        IQueryable<Vehicle> query = vehicles;

        if (filter.UnitId is { } unitId)
        {
            query = query.Where(v => v.UnitId == unitId);
        }

        if (filter.Search is { } search)
        {
            string text = $"%{EscapeLike(search)}%";
            string plate = $"%{EscapeLike(PlateFragment(search))}%";
            bool searchesPlate = PlateFragment(search).Length > 0;

            query = query.Where(v =>
                (searchesPlate && EF.Functions.ILike(v.Plate, plate, "\\"))
                || EF.Functions.ILike(v.Model, text, "\\")
                || EF.Functions.ILike(v.Color, text, "\\"));
        }

        return query;
    }

    /// <summary>O que o síndico digitou, como a placa é gravada: só letras e números, em maiúsculas ("abc-12" vira "ABC12").</summary>
    private static string PlateFragment(string search) => string.Concat(search.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();

    /// <summary>Faz % e _ valerem como caracteres comuns na busca (ILIKE os trata como curingas).</summary>
    private static string EscapeLike(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: VehicleConfiguration.PlateIndex })
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(VehicleErrors.PlateAlreadyRegistered);
        }
    }

    private string CurrentVersion(Vehicle vehicle) =>
        db.Entry(vehicle).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);

    /// <summary>Uma linha lida: o veículo, a unidade e a versão (<c>xmin</c>) no mesmo resultado.</summary>
    private sealed record VehicleRow(Vehicle Vehicle, string? UnitBlock, string UnitNumber, uint Version)
    {
        public VehicleRecord ToRecord() => new(Vehicle, UnitBlock, UnitNumber, Version.ToString(CultureInfo.InvariantCulture));
    }
}
