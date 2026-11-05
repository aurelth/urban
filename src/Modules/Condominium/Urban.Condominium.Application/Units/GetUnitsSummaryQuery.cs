using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

public sealed record GetUnitsSummaryQuery : IRequest<Result<UnitsSummary>>;
