namespace Urban.Api.Endpoints;

/// <summary>Um veículo como a tela o vê. A placa vem normalizada (maiúsculas, sem hífen: ABC1234 ou ABC1D23).</summary>
public sealed record VehicleResponse(
    Guid Id,
    Guid UnitId,
    string? UnitBlock,
    string UnitNumber,
    string Plate,
    string Model,
    string Color,
    string Version);

public sealed record VehicleListResponse(IReadOnlyList<VehicleResponse> Items, int Total, int Page, int PageSize);
