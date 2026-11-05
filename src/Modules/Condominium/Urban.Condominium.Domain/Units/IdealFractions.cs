using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Units;

/// <summary>A regra da soma das frações ideais: as unidades ativas, juntas, não passam de 100% (1 em decimal).</summary>
public static class IdealFractions
{
    public const decimal Whole = 1m;

    /// <summary>Soma das frações das unidades ativas. Desativadas e sem fração não entram. Em <c>decimal</c>, então três terços não estouram por arredondamento.</summary>
    public static decimal TotalOf(IEnumerable<CondominiumUnit> units) =>
        units.Where(u => u.IsActive).Sum(u => u.IdealFraction ?? 0m);

    public static Result EnsureWithinWhole(decimal total) =>
        total <= Whole ? Result.Success() : Result.Failure(UnitErrors.FractionSumExceeded);
}
