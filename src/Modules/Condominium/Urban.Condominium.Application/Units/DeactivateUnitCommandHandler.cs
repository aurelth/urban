using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>Desativa a unidade e guarda a data. Sair da soma das frações só libera espaço, então não há o que conferir.</summary>
public sealed class DeactivateUnitCommandHandler(IUnits units, ITenantContext tenant, IAtomicWork atomicWork, IClock clock)
    : IRequestHandler<DeactivateUnitCommand, Result<UnitView>>
{
    public async Task<Result<UnitView>> Handle(DeactivateUnitCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<UnitView>(CondominiumErrors.NotFound);
        }

        Result<UnitRecord> found = await UnitStatus.FindForChangeAsync(units, request.UnitId, request.Version, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<UnitView>(found.Error);
        }

        UnitRecord record = found.Value;
        if (!record.Unit.IsActive)
        {
            return Result.Failure<UnitView>(UnitErrors.AlreadyDeactivated);
        }

        DateTimeOffset now = clock.UtcNow;
        ChangeLogEntry entry = UnitChanges.Entry(condominiumId, request.ActorUserId, ["deactivated"], now, record.Unit.Id);

        return await UnitChanges.InAtomicWorkAsync(
            atomicWork,
            async ct =>
            {
                Result deactivated = record.Unit.Deactivate(now);
                if (deactivated.IsFailure)
                {
                    return Result.Failure<UnitView>(deactivated.Error);
                }

                return await UnitStatus.SaveAsync(units, record, entry, ct);
            },
            cancellationToken);
    }
}
