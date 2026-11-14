using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Residents;

/// <summary>Os moradores do condomínio da requisição, em ordem de nome, com filtros e página. A ordem e o recorte são do banco.</summary>
public sealed class ListResidentsQueryHandler(IResidents residents, ITenantContext tenant) : IRequestHandler<ListResidentsQuery, Result<ResidentListing>>
{
    public const int MaxPageSize = 200;

    public async Task<Result<ResidentListing>> Handle(ListResidentsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ResidentListing>(CondominiumErrors.NotFound);
        }

        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            return Result.Failure<ResidentListing>(ResidentErrors.PageInvalid);
        }

        ResidentFilter filter = new(string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(), request.UnitId, request.Status);
        PagedResidents page = await residents.ListAsync(filter, request.Page, request.PageSize, cancellationToken);

        return Result.Success(new ResidentListing([.. page.Items.Select(ResidentView.From)], page.Total, request.Page, request.PageSize));
    }
}
