using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Vehicles;

public sealed record ListVehiclesQuery(Guid? UnitId = null, string? Search = null, int Page = 1, int PageSize = 50) : IRequest<Result<VehicleListing>>;
