using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>
/// Edita uma unidade. Valida e compara em memória; se nada mudou, não grava nada (nem a versão muda). O registro leva só
/// os nomes dos campos que mudaram.
/// </summary>
public sealed class UpdateUnitCommandHandler(IUnits units, ITenantContext tenant, IAtomicWork atomicWork, IClock clock)
    : IRequestHandler<UpdateUnitCommand, Result<UnitView>>
{
    public async Task<Result<UnitView>> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<UnitView>(CondominiumErrors.NotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<UnitView>(UnitErrors.VersionRequired);
        }

        UnitRecord? record = await units.FindAsync(request.UnitId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<UnitView>(UnitErrors.NotFound);
        }

        if (request.Version != record.Version)
        {
            return Result.Failure<UnitView>(CondominiumErrors.ConcurrencyConflict);
        }

        if (!record.Unit.IsActive)
        {
            return Result.Failure<UnitView>(UnitErrors.Deactivated);
        }

        Result<UnitData> data = CondominiumUnit.Normalize(request.Block, request.Number, request.Type, request.IdealFraction, request.Area);
        if (data.IsFailure)
        {
            return Result.Failure<UnitView>(data.Error);
        }

        List<string> changed = UnitChanges.Changed(record.Unit, data.Value);
        if (changed.Count == 0)
        {
            return Result.Success(UnitView.From(record));
        }

        ChangeLogEntry entry = UnitChanges.Entry(condominiumId, request.ActorUserId, changed, clock.UtcNow, record.Unit.Id);

        return await UnitChanges.InAtomicWorkAsync(atomicWork, ct => ApplyAsync(record, data.Value, changed, entry, ct), cancellationToken);
    }

    private async Task<Result<UnitView>> ApplyAsync(UnitRecord record, UnitData data, List<string> changed, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        CondominiumUnit unit = record.Unit;

        bool identityChanged = changed.Contains("block") || changed.Contains("number");
        if (identityChanged && await units.IsIdentityTakenAsync(new UnitIdentity(data.Block, data.Number), unit.Id, cancellationToken))
        {
            return Result.Failure<UnitView>(UnitErrors.IdentifierAlreadyExists);
        }

        if (changed.Contains("idealFraction") && data.IdealFraction is { } fraction)
        {
            Result fits = await UnitChanges.EnsureFractionFitsAsync(units, unit.Id, fraction, cancellationToken);
            if (fits.IsFailure)
            {
                return Result.Failure<UnitView>(fits.Error);
            }
        }

        Result updated = unit.Update(data.Block, data.Number, data.Type, data.IdealFraction, data.Area);
        if (updated.IsFailure)
        {
            return Result.Failure<UnitView>(updated.Error);
        }

        Result<string> saved = await units.SaveAsync(record, entry, cancellationToken);

        return saved.IsSuccess ? Result.Success(UnitView.From(unit, saved.Value)) : Result.Failure<UnitView>(saved.Error);
    }
}
