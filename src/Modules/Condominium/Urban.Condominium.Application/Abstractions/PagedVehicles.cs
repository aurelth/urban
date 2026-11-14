namespace Urban.Condominium.Application.Abstractions;

/// <summary>Uma página de veículos, já na ordem da lista, e quantos o filtro encontrou ao todo.</summary>
public sealed record PagedVehicles(IReadOnlyList<VehicleRecord> Items, int Total);
