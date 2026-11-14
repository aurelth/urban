using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>O que os handlers de veículos têm em comum: os nomes dos campos do registro e a conferência da unidade.</summary>
internal static class VehicleChanges
{
    /// <summary>Nomes dos campos preenchidos ao cadastrar: todos são obrigatórios.</summary>
    public static readonly string[] FilledIn = ["unit", "plate", "model", "color"];

    /// <summary>Nomes dos campos que mudaram de um veículo para os dados novos (já normalizados).</summary>
    public static List<string> Changed(Vehicle vehicle, Guid unitId, VehicleData data)
    {
        List<string> fields = [];
        AddIf(fields, "unit", unitId != vehicle.UnitId);
        AddIf(fields, "plate", data.Plate != vehicle.Plate);
        AddIf(fields, "model", data.Model != vehicle.Model);
        AddIf(fields, "color", data.Color != vehicle.Color);

        return fields;
    }

    public static ChangeLogEntry Entry(Guid condominiumId, Guid actorUserId, IReadOnlyList<string> fields, DateTimeOffset at, Guid vehicleId) =>
        new(condominiumId, actorUserId, ChangeKind.Vehicle, fields, at, vehicleId);

    /// <summary>A unidade que vai receber o veículo: precisa existir neste condomínio e estar ativa.</summary>
    public static async Task<Result<UnitRecord>> ActiveUnitAsync(IUnits units, Guid unitId, CancellationToken cancellationToken)
    {
        UnitRecord? unit = await units.FindAsync(unitId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure<UnitRecord>(UnitErrors.NotFound);
        }

        return unit.Unit.IsActive ? Result.Success(unit) : Result.Failure<UnitRecord>(UnitErrors.Deactivated);
    }

    private static void AddIf(List<string> fields, string name, bool condition)
    {
        if (condition)
        {
            fields.Add(name);
        }
    }
}
