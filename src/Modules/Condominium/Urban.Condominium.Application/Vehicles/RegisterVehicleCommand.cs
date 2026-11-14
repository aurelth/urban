using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>O condomínio não vem no comando: é o do token, e o síndico só cadastra no dele.</summary>
public sealed record RegisterVehicleCommand(Guid ActorUserId, Guid UnitId, string? Plate, string? Model, string? Color) : IRequest<Result<VehicleView>>;
