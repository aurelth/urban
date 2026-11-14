using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Vehicles;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>Um veículo como a tela o vê, com o bloco e o número da unidade.</summary>
/// <param name="Version">Devolvida na edição para detectar que outra pessoa mudou o veículo no meio do caminho.</param>
public sealed record VehicleView(
    Guid Id,
    Guid UnitId,
    string? UnitBlock,
    string UnitNumber,
    string Plate,
    string Model,
    string Color,
    string Version)
{
    internal static VehicleView From(Vehicle vehicle, string? unitBlock, string unitNumber, string version) =>
        new(vehicle.Id, vehicle.UnitId, unitBlock, unitNumber, vehicle.Plate, vehicle.Model, vehicle.Color, version);

    internal static VehicleView From(VehicleRecord record) =>
        From(record.Vehicle, record.UnitBlock, record.UnitNumber, record.Version);
}
