using Urban.SharedKernel;

namespace Urban.Finance.Domain.Consumption;

/// <summary>
/// Tabela de faixas progressivas de uma tarifa (a da água ou a do esgoto). Cada faixa cobra só os m³ que caem nela,
/// como a tabela da companhia: a primeira costuma ser um valor fixo até 10 m³ e as seguintes, um preço por m³.
/// </summary>
public sealed class TariffScale
{
    public const int MaximumBands = 6;
    public const decimal MaximumLimit = 100_000m;
    public const decimal MaximumPrice = 9_999.9999m;

    private TariffScale(IReadOnlyList<TariffBand> bands)
    {
        Bands = bands;
    }

    public IReadOnlyList<TariffBand> Bands { get; }

    public static Result<TariffScale> Create(IReadOnlyList<TariffBand> bands)
    {
        if (bands.Count is 0 or > MaximumBands || !HasValidLimits(bands) || !bands.All(IsValidBand))
        {
            return Result.Failure<TariffScale>(ConsumptionErrors.BandsInvalid);
        }

        return Result.Success(new TariffScale([.. bands]));
    }

    /// <summary>Quanto custa o consumo em m³, em centavos.</summary>
    public decimal ChargeFor(decimal cubicMeters) => MoneyRules.Round(RawChargeFor(cubicMeters));

    /// <summary>O mesmo valor sem arredondar, para quem ainda vai multiplicar por um percentual antes de arredondar.</summary>
    internal decimal RawChargeFor(decimal cubicMeters)
    {
        decimal consumption = Math.Max(0m, cubicMeters);
        decimal total = 0m;
        decimal lower = 0m;

        for (int index = 0; index < Bands.Count; index++)
        {
            TariffBand band = Bands[index];

            if (index == 0 || consumption > lower)
            {
                total += band.FixedAmount;
            }

            decimal upper = band.UpToCubicMeters ?? decimal.MaxValue;
            total += Math.Max(0m, Math.Min(consumption, upper) - lower) * band.PricePerCubicMeter;
            lower = upper;
        }

        return total;
    }

    private static bool HasValidLimits(IReadOnlyList<TariffBand> bands)
    {
        decimal previous = 0m;

        for (int index = 0; index < bands.Count; index++)
        {
            bool isLast = index == bands.Count - 1;
            decimal? limit = bands[index].UpToCubicMeters;

            if (isLast != limit is null)
            {
                return false;
            }

            if (limit is { } value)
            {
                if (value <= previous || value > MaximumLimit || !MoneyRules.HasAtMostDecimals(value, 3))
                {
                    return false;
                }

                previous = value;
            }
        }

        return true;
    }

    private static bool IsValidBand(TariffBand band) =>
        band.FixedAmount >= 0
        && band.FixedAmount <= MoneyRules.Maximum
        && MoneyRules.HasAtMostDecimals(band.FixedAmount, MoneyRules.DecimalPlaces)
        && band.PricePerCubicMeter >= 0
        && band.PricePerCubicMeter <= MaximumPrice
        && MoneyRules.HasAtMostDecimals(band.PricePerCubicMeter, 4);
}
