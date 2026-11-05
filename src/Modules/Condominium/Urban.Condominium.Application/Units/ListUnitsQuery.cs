using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

public sealed record ListUnitsQuery(
    string? Block = null,
    UnitType? Type = null,
    UnitStatusFilter Status = UnitStatusFilter.Active,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<UnitListing>>;
