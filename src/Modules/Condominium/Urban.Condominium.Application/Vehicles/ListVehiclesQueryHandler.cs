using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Vehicles;

/// <summary>Os veículos do condomínio da requisição, por unidade e placa, com filtros e página. A ordem e o recorte são do banco.</summary>
public sealed class ListVehiclesQueryHandler(IVehicles vehicles, ITenantContext tenant) : IRequestHandler<ListVehiclesQuery, Result<VehicleListing>>
{
    public const int MaxPageSize = 200;

    public async Task<Result<VehicleListing>> Handle(ListVehiclesQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<VehicleListing>(CondominiumErrors.NotFound);
        }

        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            return Result.Failure<VehicleListing>(VehicleErrors.PageInvalid);
        }

        VehicleFilter filter = new(request.UnitId, string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim());
        PagedVehicles page = await vehicles.ListAsync(filter, request.Page, request.PageSize, cancellationToken);

        return Result.Success(new VehicleListing([.. page.Items.Select(VehicleView.From)], page.Total, request.Page, request.PageSize));
    }
}
