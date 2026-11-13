using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

public sealed record ListResidentsQuery(string? Search = null, Guid? UnitId = null, int Page = 1, int PageSize = 50) : IRequest<Result<ResidentListing>>;
