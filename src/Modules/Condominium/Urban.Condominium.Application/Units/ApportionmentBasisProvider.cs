using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Application.Units;

/// <summary>
/// Cumpre o contrato do rateio: lê o método escolhido e as unidades, confere com a mesma regra de prontidão que a tela de
/// unidades usa e devolve só as ativas, na ordem natural, com o peso de cada uma. O Financeiro soma e reparte; a regra de
/// "quem entra e quanto pesa" fica aqui, junto dos dados das unidades.
/// </summary>
public sealed class ApportionmentBasisProvider(IUnits units, IApportionments apportionments, ITenantContext tenant) : IApportionmentBasis
{
    public async Task<Result<ApportionmentBasis>> GetAsync(CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ApportionmentBasis>(CondominiumErrors.NotFound);
        }

        ApportionmentSetting? setting = await apportionments.FindAsync(condominiumId, cancellationToken);
        if (setting is null)
        {
            return Result.Failure<ApportionmentBasis>(CondominiumErrors.NotFound);
        }

        ApportionmentMethod method = setting.Condominium.ApportionmentMethod;
        List<CondominiumUnit> active = [.. (await units.AllAsync(cancellationToken)).Where(unit => unit.IsActive)];

        if (ApportionmentReadiness.Diagnose(method, active) is { } shortfall)
        {
            return Result.Failure<ApportionmentBasis>(CondominiumErrors.ApportionmentNotReadyFor(shortfall));
        }

        return Result.Success(new ApportionmentBasis(
            KindOf(method),
            [.. active.Select(unit => new ApportionmentUnit(unit.Id, unit.Block, unit.Number, WeightOf(method, unit)))]));
    }

    private static ApportionmentBasisKind KindOf(ApportionmentMethod method) => method switch
    {
        ApportionmentMethod.IdealFraction => ApportionmentBasisKind.IdealFraction,
        ApportionmentMethod.Area => ApportionmentBasisKind.Area,
        _ => ApportionmentBasisKind.EqualShare,
    };

    private static decimal WeightOf(ApportionmentMethod method, CondominiumUnit unit) => method switch
    {
        ApportionmentMethod.IdealFraction => unit.IdealFraction ?? 0m,
        ApportionmentMethod.Area => unit.Area ?? 0m,
        _ => 1m,
    };
}
