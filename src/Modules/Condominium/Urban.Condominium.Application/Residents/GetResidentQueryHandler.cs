using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Residents;

/// <summary>Um morador do condomínio da requisição. O de outro condomínio é "não encontrado", igual a um que não existe.</summary>
public sealed class GetResidentQueryHandler(IResidents residents, ITenantContext tenant, ResidentViews views) : IRequestHandler<GetResidentQuery, Result<ResidentView>>
{
    public async Task<Result<ResidentView>> Handle(GetResidentQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ResidentView>(CondominiumErrors.NotFound);
        }

        ResidentRecord? record = await residents.FindAsync(request.ResidentId, cancellationToken);

        return record is null
            ? Result.Failure<ResidentView>(ResidentErrors.NotFound)
            : Result.Success(await views.ViewAsync(record, cancellationToken));
    }
}
