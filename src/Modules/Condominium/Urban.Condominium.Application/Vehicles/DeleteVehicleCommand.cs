using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Vehicles;

public sealed record DeleteVehicleCommand(Guid ActorUserId, Guid VehicleId) : IRequest<Result>;
