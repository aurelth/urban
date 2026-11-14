using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Vehicles;

public sealed record GetVehicleQuery(Guid VehicleId) : IRequest<Result<VehicleView>>;
