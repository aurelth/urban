using Urban.Finance.Domain.Consumption;

namespace Urban.Finance.Application.Consumption;

internal static class ConsumptionTariffViews
{
    public static ConsumptionTariffView From(ConsumptionTariff tariff) =>
        new(
            tariff.ValidFrom.ToString(),
            Bands(tariff.Water),
            tariff.Sewage is null ? null : Bands(tariff.Sewage),
            tariff.SewagePercent,
            tariff.Gas is null ? null : new GasTariffView(tariff.Gas.KilosPerCubicMeter, tariff.Gas.PricePerKilo, tariff.Gas.PricePerCubicMeter));

    private static List<TariffBandView> Bands(TariffScale scale) =>
        [.. scale.Bands.Select(band => new TariffBandView(band.UpToCubicMeters, band.FixedAmount, band.PricePerCubicMeter))];
}
