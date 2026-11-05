using MediatR;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

/// <param name="Version">A versão que a tela leu; sem ela a edição não sabe o que está substituindo.</param>
public sealed record UpdateUnitCommand(
    Guid ActorUserId,
    Guid UnitId,
    string? Version,
    string? Block,
    string? Number,
    UnitType Type,
    decimal? IdealFraction,
    decimal? Area) : IRequest<Result<UnitView>>;
