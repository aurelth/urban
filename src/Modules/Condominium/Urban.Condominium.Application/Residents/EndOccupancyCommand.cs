using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

/// <summary>Registra que o morador saiu da unidade. O vínculo não é apagado: ganha a data de saída e fica no histórico.</summary>
public sealed record EndOccupancyCommand(
    Guid ActorUserId,
    Guid ResidentId,
    Guid OccupancyId,
    DateOnly? EndDate) : IRequest<Result<ResidentView>>;
