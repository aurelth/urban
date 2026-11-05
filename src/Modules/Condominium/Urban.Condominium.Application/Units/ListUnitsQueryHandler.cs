using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>A lista de unidades do condomínio da requisição, na ordem natural, com filtros e página. A ordem e o recorte são do banco.</summary>
public sealed class ListUnitsQueryHandler(IUnits units, ITenantContext tenant) : IRequestHandler<ListUnitsQuery, Result<UnitListing>>
{
    public const int MaxPageSize = 200;

    public async Task<Result<UnitListing>> Handle(ListUnitsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<UnitListing>(CondominiumErrors.NotFound);
        }

        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            return Result.Failure<UnitListing>(UnitErrors.PageInvalid);
        }

        UnitFilter filter = new(NullIfBlank(request.Block), request.Type, request.Status, NullIfBlank(request.Search));
        PagedUnits page = await units.ListAsync(filter, request.Page, request.PageSize, cancellationToken);

        return Result.Success(new UnitListing([.. page.Items.Select(UnitView.From)], page.Total, request.Page, request.PageSize));
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
