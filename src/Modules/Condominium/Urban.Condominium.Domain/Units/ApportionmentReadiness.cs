using Urban.Condominium.Domain.Condominiums;

namespace Urban.Condominium.Domain.Units;

/// <summary>Diz se os dados das unidades já bastam para um método de rateio funcionar.</summary>
public static class ApportionmentReadiness
{
    /// <summary>
    /// Margem aceita na soma das frações: coeficientes escritos em percentual com 4 casas, somados em um prédio grande,
    /// raramente fecham em 100% exato. 0,0001 (0,01 ponto percentual) cobre o arredondamento de uns 200 coeficientes.
    /// </summary>
    public const decimal FractionTolerance = 0.0001m;

    public static bool IsReady(ApportionmentMethod method, IReadOnlyCollection<CondominiumUnit> units)
    {
        List<CondominiumUnit> active = [.. units.Where(u => u.IsActive)];
        if (active.Count == 0)
        {
            return false;
        }

        return method switch
        {
            ApportionmentMethod.IdealFraction => active.All(u => u.IdealFraction is not null) && IdealFractions.Whole - IdealFractions.TotalOf(active) <= FractionTolerance,
            ApportionmentMethod.EqualShare => true,
            ApportionmentMethod.Area => active.All(u => u.Area is not null),
            _ => false,
        };
    }
}
