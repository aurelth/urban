using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

public sealed record ListResidentsQuery(string? Search = null, Guid? UnitId = null, int Page = 1, int PageSize = 50, ResidentStatusFilter Status = ResidentStatusFilter.All) : IRequest<Result<ResidentListing>>;
