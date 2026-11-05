using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>Uma unidade do condomínio da requisição. Uma unidade de outro condomínio não aparece: para quem pede, ela não existe.</summary>
public sealed class GetUnitQueryHandler(IUnits units, ITenantContext tenant) : IRequestHandler<GetUnitQuery, Result<UnitView>>
{
    public async Task<Result<UnitView>> Handle(GetUnitQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<UnitView>(CondominiumErrors.NotFound);
        }

        UnitRecord? record = await units.FindAsync(request.UnitId, cancellationToken);

        return record is null ? Result.Failure<UnitView>(UnitErrors.NotFound) : Result.Success(UnitView.From(record));
    }
}
