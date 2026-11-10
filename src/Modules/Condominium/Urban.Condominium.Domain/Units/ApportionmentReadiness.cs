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

    public static bool IsReady(ApportionmentMethod method, IReadOnlyCollection<CondominiumUnit> units) =>
        Diagnose(method, units) is null;

    /// <summary>O que falta para o método funcionar com estas unidades, ou nada quando já basta. Só as unidades ativas contam.</summary>
    public static ApportionmentShortfall? Diagnose(ApportionmentMethod method, IReadOnlyCollection<CondominiumUnit> units)
    {
        List<CondominiumUnit> active = [.. units.Where(u => u.IsActive)];
        if (active.Count == 0)
        {
            return ApportionmentShortfall.NoActiveUnits();
        }

        return method switch
        {
            ApportionmentMethod.IdealFraction => DiagnoseFractions(active),
            ApportionmentMethod.EqualShare => null,
            ApportionmentMethod.Area => DiagnoseAreas(active),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Método de rateio desconhecido"),
        };
    }

    private static ApportionmentShortfall? DiagnoseFractions(List<CondominiumUnit> active)
    {
        List<UnitReference> missing = ReferencesOf(active, u => u.IdealFraction is null);
        if (missing.Count > 0)
        {
            return ApportionmentShortfall.FractionsMissing(missing);
        }

        decimal total = IdealFractions.TotalOf(active);
        return IdealFractions.Whole - total <= FractionTolerance ? null : ApportionmentShortfall.FractionsTotal(total);
    }

    private static ApportionmentShortfall? DiagnoseAreas(List<CondominiumUnit> active)
    {
        List<UnitReference> missing = ReferencesOf(active, u => u.Area is null);
        return missing.Count > 0 ? ApportionmentShortfall.AreasMissing(missing) : null;
    }

    private static List<UnitReference> ReferencesOf(List<CondominiumUnit> active, Func<CondominiumUnit, bool> lacksTheValue) =>
        [.. active.Where(lacksTheValue).Select(u => new UnitReference(u.Block, u.Number))];
}
