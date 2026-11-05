using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>
/// Cadastra uma unidade no condomínio da requisição. Valida tudo em memória antes de falar com o banco; depois confere
/// o identificador e a soma das frações para dar uma resposta clara. A conferência de verdade (que resiste a dois
/// cadastros ao mesmo tempo) fica na gravação.
/// </summary>
public sealed class CreateUnitCommandHandler(IUnits units, ITenantContext tenant, IAtomicWork atomicWork, IClock clock)
    : IRequestHandler<CreateUnitCommand, Result<UnitView>>
{
    public async Task<Result<UnitView>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<UnitView>(CondominiumErrors.NotFound);
        }

        Result<CondominiumUnit> created = CondominiumUnit.Create(condominiumId, request.Block, request.Number, request.Type, request.IdealFraction, request.Area, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<UnitView>(created.Error);
        }

        CondominiumUnit unit = created.Value;
        List<string> fields = UnitChanges.FilledIn(new UnitData(unit.Block, unit.Number, unit.Type, unit.IdealFraction, unit.Area));
        ChangeLogEntry entry = UnitChanges.Entry(condominiumId, request.ActorUserId, fields, clock.UtcNow, unit.Id);

        return await UnitChanges.InAtomicWorkAsync(atomicWork, ct => AddAsync(unit, entry, ct), cancellationToken);
    }

    private async Task<Result<UnitView>> AddAsync(CondominiumUnit unit, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        if (await units.IsIdentityTakenAsync(unit.Identity, null, cancellationToken))
        {
            return Result.Failure<UnitView>(UnitErrors.IdentifierAlreadyExists);
        }

        if (unit.IdealFraction is { } fraction)
        {
            Result fits = await UnitChanges.EnsureFractionFitsAsync(units, null, fraction, cancellationToken);
            if (fits.IsFailure)
            {
                return Result.Failure<UnitView>(fits.Error);
            }
        }

        Result<string> saved = await units.AddAsync(unit, entry, cancellationToken);

        return saved.IsSuccess ? Result.Success(UnitView.From(unit, saved.Value)) : Result.Failure<UnitView>(saved.Error);
    }
}
