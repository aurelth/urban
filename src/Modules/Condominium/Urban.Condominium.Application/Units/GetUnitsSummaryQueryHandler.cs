using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>
/// O resumo que a tela de unidades mostra no topo: contagens, a soma das frações, o que falta preencher e se cada método
/// de rateio já pode ser usado. Lê todas as unidades de uma vez: é pouco dado e as contas são em memória, com as mesmas
/// regras do Domain.
/// </summary>
public sealed class GetUnitsSummaryQueryHandler(IUnits units, IApportionments apportionments, ITenantContext tenant)
    : IRequestHandler<GetUnitsSummaryQuery, Result<UnitsSummary>>
{
    private static readonly ApportionmentMethod[] Methods = [ApportionmentMethod.IdealFraction, ApportionmentMethod.EqualShare, ApportionmentMethod.Area];

    public async Task<Result<UnitsSummary>> Handle(GetUnitsSummaryQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<UnitsSummary>(CondominiumErrors.NotFound);
        }

        ApportionmentSetting? setting = await apportionments.FindAsync(condominiumId, cancellationToken);
        if (setting is null)
        {
            return Result.Failure<UnitsSummary>(CondominiumErrors.NotFound);
        }

        IReadOnlyList<CondominiumUnit> all = await units.AllAsync(cancellationToken);
        List<CondominiumUnit> active = [.. all.Where(u => u.IsActive)];

        return Result.Success(new UnitsSummary(
            Active: active.Count,
            Deactivated: all.Count - active.Count,
            Houses: active.Count(u => u.Type == UnitType.House),
            Apartments: active.Count(u => u.Type == UnitType.Apartment),
            IdealFractionTotal: IdealFractions.TotalOf(active),
            WithoutIdealFraction: active.Count(u => u.IdealFraction is null),
            WithoutArea: active.Count(u => u.Area is null),
            Method: setting.Condominium.ApportionmentMethod,
            ApportionmentVersion: setting.Version,
            Readiness: [.. Methods.Select(method => new MethodReadiness(method, ApportionmentReadiness.IsReady(method, active)))]));
    }
}
