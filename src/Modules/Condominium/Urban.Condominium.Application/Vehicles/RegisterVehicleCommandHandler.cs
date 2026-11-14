using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>
/// Cadastra um veículo numa unidade do condomínio da requisição. Valida tudo em memória antes de falar com o banco; depois
/// confere a unidade (existe aqui e está ativa) e a placa para dar uma resposta clara. A conferência de verdade da placa,
/// que resiste a dois cadastros ao mesmo tempo, é o índice único do banco, na gravação.
/// </summary>
public sealed class RegisterVehicleCommandHandler(IVehicles vehicles, IUnits units, ITenantContext tenant, IClock clock)
    : IRequestHandler<RegisterVehicleCommand, Result<VehicleView>>
{
    public async Task<Result<VehicleView>> Handle(RegisterVehicleCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<VehicleView>(CondominiumErrors.NotFound);
        }

        DateTimeOffset now = clock.UtcNow;
        Result<Vehicle> created = Vehicle.Create(condominiumId, request.UnitId, request.Plate, request.Model, request.Color, now);
        if (created.IsFailure)
        {
            return Result.Failure<VehicleView>(created.Error);
        }

        Vehicle vehicle = created.Value;
        Result<UnitRecord> unit = await VehicleChanges.ActiveUnitAsync(units, vehicle.UnitId, cancellationToken);
        if (unit.IsFailure)
        {
            return Result.Failure<VehicleView>(unit.Error);
        }

        if (await vehicles.IsPlateTakenAsync(vehicle.Plate, null, cancellationToken))
        {
            return Result.Failure<VehicleView>(VehicleErrors.PlateAlreadyRegistered);
        }

        ChangeLogEntry entry = VehicleChanges.Entry(condominiumId, request.ActorUserId, VehicleChanges.FilledIn, now, vehicle.Id);
        Result<string> saved = await vehicles.AddAsync(vehicle, entry, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(VehicleView.From(vehicle, unit.Value.Unit.Block, unit.Value.Unit.Number, saved.Value))
            : Result.Failure<VehicleView>(saved.Error);
    }
}
