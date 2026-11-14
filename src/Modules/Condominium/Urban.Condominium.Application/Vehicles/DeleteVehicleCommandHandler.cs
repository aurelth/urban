using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>
/// Apaga um veículo (a linha some e a placa fica livre). Ao contrário da unidade, o veículo não tem histórico que dependa dele:
/// nenhuma cobrança nem ocupação aponta para ele. O registro de alterações guarda que ele foi removido, sem nenhum valor.
/// </summary>
public sealed class DeleteVehicleCommandHandler(IVehicles vehicles, ITenantContext tenant, IClock clock)
    : IRequestHandler<DeleteVehicleCommand, Result>
{
    private static readonly string[] Removed = ["removed"];

    public async Task<Result> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure(CondominiumErrors.NotFound);
        }

        VehicleRecord? record = await vehicles.FindAsync(request.VehicleId, cancellationToken);
        if (record is null)
        {
            return Result.Failure(VehicleErrors.NotFound);
        }

        ChangeLogEntry entry = VehicleChanges.Entry(condominiumId, request.ActorUserId, Removed, clock.UtcNow, record.Vehicle.Id);

        return await vehicles.DeleteAsync(record, entry, cancellationToken);
    }
}
