using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>Um veículo do condomínio da requisição. Um veículo de outro condomínio não aparece: para quem pede, ele não existe.</summary>
public sealed class GetVehicleQueryHandler(IVehicles vehicles, ITenantContext tenant) : IRequestHandler<GetVehicleQuery, Result<VehicleView>>
{
    public async Task<Result<VehicleView>> Handle(GetVehicleQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<VehicleView>(CondominiumErrors.NotFound);
        }

        VehicleRecord? record = await vehicles.FindAsync(request.VehicleId, cancellationToken);

        return record is null ? Result.Failure<VehicleView>(VehicleErrors.NotFound) : Result.Success(VehicleView.From(record));
    }
}
