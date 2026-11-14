using MediatR;
using Urban.Condominium.Domain.Occupancies;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

/// <summary>Um novo vínculo de um morador que já existe com uma unidade (proprietário que também aluga outra, morador que se muda).</summary>
public sealed record AddOccupancyCommand(
    Guid ActorUserId,
    Guid ResidentId,
    Guid UnitId,
    OccupancyKind Kind,
    DateOnly? StartDate) : IRequest<Result<ResidentView>>;
