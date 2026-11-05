using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

public sealed record GetUnitQuery(Guid UnitId) : IRequest<Result<UnitView>>;
