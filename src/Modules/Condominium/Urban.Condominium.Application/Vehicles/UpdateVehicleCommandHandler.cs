using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>
/// Edita um veículo. Valida e compara em memória; se nada mudou, não grava nada (nem a versão muda). O registro leva só os
/// nomes dos campos que mudaram. Mudar de unidade exige uma unidade ativa do condomínio; editar o resto de um veículo que
/// está numa unidade desativada é permitido.
/// </summary>
public sealed class UpdateVehicleCommandHandler(IVehicles vehicles, IUnits units, ITenantContext tenant, IClock clock)
    : IRequestHandler<UpdateVehicleCommand, Result<VehicleView>>
{
    public async Task<Result<VehicleView>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<VehicleView>(CondominiumErrors.NotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<VehicleView>(VehicleErrors.VersionRequired);
        }

        VehicleRecord? record = await vehicles.FindAsync(request.VehicleId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<VehicleView>(VehicleErrors.NotFound);
        }

        if (request.Version != record.Version)
        {
            return Result.Failure<VehicleView>(CondominiumErrors.ConcurrencyConflict);
        }

        if (request.UnitId == Guid.Empty)
        {
            return Result.Failure<VehicleView>(VehicleErrors.UnitRequired);
        }

        Result<VehicleData> data = Vehicle.Normalize(request.Plate, request.Model, request.Color);
        if (data.IsFailure)
        {
            return Result.Failure<VehicleView>(data.Error);
        }

        Vehicle vehicle = record.Vehicle;
        List<string> changed = VehicleChanges.Changed(vehicle, request.UnitId, data.Value);
        if (changed.Count == 0)
        {
            return Result.Success(VehicleView.From(record));
        }

        string? unitBlock = record.UnitBlock;
        string unitNumber = record.UnitNumber;
        if (changed.Contains("unit"))
        {
            Result<UnitRecord> target = await VehicleChanges.ActiveUnitAsync(units, request.UnitId, cancellationToken);
            if (target.IsFailure)
            {
                return Result.Failure<VehicleView>(target.Error);
            }

            unitBlock = target.Value.Unit.Block;
            unitNumber = target.Value.Unit.Number;
        }

        if (changed.Contains("plate") && await vehicles.IsPlateTakenAsync(data.Value.Plate, vehicle.Id, cancellationToken))
        {
            return Result.Failure<VehicleView>(VehicleErrors.PlateAlreadyRegistered);
        }

        Result updated = vehicle.Update(request.UnitId, request.Plate, request.Model, request.Color);
        if (updated.IsFailure)
        {
            return Result.Failure<VehicleView>(updated.Error);
        }

        ChangeLogEntry entry = VehicleChanges.Entry(condominiumId, request.ActorUserId, changed, clock.UtcNow, vehicle.Id);
        Result<string> saved = await vehicles.SaveAsync(record, entry, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(VehicleView.From(vehicle, unitBlock, unitNumber, saved.Value))
            : Result.Failure<VehicleView>(saved.Error);
    }
}
