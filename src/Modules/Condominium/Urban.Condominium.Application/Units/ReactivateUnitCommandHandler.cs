using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>
/// Reativa a unidade. O bloco e o número continuaram reservados enquanto ela esteve desativada, então só a fração pode
/// atrapalhar: ela volta para a soma e a soma não pode passar de 100%.
/// </summary>
public sealed class ReactivateUnitCommandHandler(IUnits units, ITenantContext tenant, IAtomicWork atomicWork, IClock clock)
    : IRequestHandler<ReactivateUnitCommand, Result<UnitView>>
{
    public async Task<Result<UnitView>> Handle(ReactivateUnitCommand request, CancellationToken cancellationToken)
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
        if (record.Unit.IsActive)
        {
            return Result.Failure<UnitView>(UnitErrors.AlreadyActive);
        }

        ChangeLogEntry entry = UnitChanges.Entry(condominiumId, request.ActorUserId, ["reactivated"], clock.UtcNow, record.Unit.Id);

        return await UnitChanges.InAtomicWorkAsync(atomicWork, ct => ReactivateAsync(record, entry, ct), cancellationToken);
    }

    private async Task<Result<UnitView>> ReactivateAsync(UnitRecord record, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        if (record.Unit.IdealFraction is { } fraction)
        {
            Result fits = await UnitChanges.EnsureFractionFitsAsync(units, record.Unit.Id, fraction, cancellationToken);
            if (fits.IsFailure)
            {
                return Result.Failure<UnitView>(fits.Error);
            }
        }

        Result reactivated = record.Unit.Reactivate();
        if (reactivated.IsFailure)
        {
            return Result.Failure<UnitView>(reactivated.Error);
        }

        return await UnitStatus.SaveAsync(units, record, entry, cancellationToken);
    }
}
