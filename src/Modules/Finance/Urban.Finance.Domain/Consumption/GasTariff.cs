using Urban.SharedKernel;

namespace Urban.Finance.Domain.Consumption;

/// <summary>
/// O gás é medido em m³ mas vendido em kg: o preço do m³ é os kg que cabem num m³ vezes o preço do kg, em centavos
/// (2,4 kg/m³ × R$ 15,71 = R$ 37,70). Quem já sabe o preço do m³ informa 1 kg por m³ e o preço dele.
/// </summary>
public sealed class GasTariff
{
    public const decimal MaximumKilosPerCubicMeter = 100m;
    public const decimal MaximumPricePerKilo = 999.99m;

    private GasTariff(decimal kilosPerCubicMeter, decimal pricePerKilo)
    {
        KilosPerCubicMeter = kilosPerCubicMeter;
        PricePerKilo = pricePerKilo;
        PricePerCubicMeter = MoneyRules.Round(kilosPerCubicMeter * pricePerKilo);
    }

    public decimal KilosPerCubicMeter { get; }

    public decimal PricePerKilo { get; }

    public decimal PricePerCubicMeter { get; }

    public static Result<GasTariff> Create(decimal kilosPerCubicMeter, decimal pricePerKilo)
    {
        bool valid = kilosPerCubicMeter > 0
            && kilosPerCubicMeter <= MaximumKilosPerCubicMeter
            && MoneyRules.HasAtMostDecimals(kilosPerCubicMeter, 3)
            && pricePerKilo > 0
            && pricePerKilo <= MaximumPricePerKilo
            && MoneyRules.HasAtMostDecimals(pricePerKilo, MoneyRules.DecimalPlaces);

        return valid
            ? Result.Success(new GasTariff(kilosPerCubicMeter, pricePerKilo))
            : Result.Failure<GasTariff>(ConsumptionErrors.GasInvalid);
    }

    public decimal ChargeFor(decimal cubicMeters) => MoneyRules.Round(Math.Max(0m, cubicMeters) * PricePerCubicMeter);
}
