using MediatR;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

/// <summary>O condomínio não vem no comando: é o do token, e o síndico só cadastra no dele. A fração vai de 0 a 1.</summary>
public sealed record CreateUnitCommand(
    Guid ActorUserId,
    string? Block,
    string? Number,
    UnitType Type,
    decimal? IdealFraction,
    decimal? Area) : IRequest<Result<UnitView>>;
