using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Vehicles;

/// <param name="Version">A versão que a tela leu; sem ela a edição não sabe o que está substituindo.</param>
public sealed record UpdateVehicleCommand(Guid ActorUserId, Guid VehicleId, string? Version, Guid UnitId, string? Plate, string? Model, string? Color) : IRequest<Result<VehicleView>>;
