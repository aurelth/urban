using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

/// <summary>Desativa a unidade. Ela nunca é apagada, porque morador e cobrança vão apontar para ela.</summary>
public sealed record DeactivateUnitCommand(Guid ActorUserId, Guid UnitId, string? Version) : IRequest<Result<UnitView>>;

/// <summary>Reativa a unidade, desde que a fração dela ainda caiba nos 100%.</summary>
public sealed record ReactivateUnitCommand(Guid ActorUserId, Guid UnitId, string? Version) : IRequest<Result<UnitView>>;
