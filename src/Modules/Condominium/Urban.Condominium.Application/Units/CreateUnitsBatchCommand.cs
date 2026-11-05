using MediatR;
using Urban.Condominium.Domain.Units;

namespace Urban.Condominium.Application.Units;

/// <summary>Cadastro em lote: de 1 a 500 unidades, tudo ou nada.</summary>
public sealed record CreateUnitsBatchCommand(Guid ActorUserId, IReadOnlyList<UnitInput> Units) : IRequest<UnitBatchResult>;

public sealed record UnitInput(string? Block, string? Number, UnitType Type, decimal? IdealFraction, decimal? Area);
