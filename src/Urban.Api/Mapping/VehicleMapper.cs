using Riok.Mapperly.Abstractions;
using Urban.Api.Endpoints;
using Urban.Condominium.Application.Vehicles;

namespace Urban.Api.Mapping;

[Mapper]
public static partial class VehicleMapper
{
    // ---- Do corpo da requisição para o comando. Unidade ausente vira vazia: quem diz que falta é o Domain, com o código do campo ----

    public static RegisterVehicleCommand ToRegisterCommand(VehicleRequest request, Guid actorUserId) =>
        new(actorUserId, request.UnitId ?? Guid.Empty, request.Plate, request.Model, request.Color);

    public static UpdateVehicleCommand ToUpdateCommand(VehicleRequest request, Guid vehicleId, Guid actorUserId) =>
        new(actorUserId, vehicleId, request.Version, request.UnitId ?? Guid.Empty, request.Plate, request.Model, request.Color);

    // ---- Do resultado para a resposta ----

    public static partial VehicleResponse ToResponse(VehicleView view);

    public static partial VehicleListResponse ToResponse(VehicleListing listing);
}
