using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Urban.Finance.Domain.Consumption;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

/// <summary>A tabela de faixas vai para uma coluna jsonb: é sempre lida e gravada inteira, nunca consultada por dentro.</summary>
internal sealed class TariffScaleConverter()
    : ValueConverter<TariffScale, string>(scale => ToJson(scale), json => FromJson(json))
{
    private sealed record BandJson(decimal? UpTo, decimal Fixed, decimal Price);

    private static string ToJson(TariffScale scale) =>
        JsonSerializer.Serialize(scale.Bands.Select(band => new BandJson(band.UpToCubicMeters, band.FixedAmount, band.PricePerCubicMeter)));

    private static TariffScale FromJson(string json) =>
        TariffScale.Create([.. JsonSerializer.Deserialize<List<BandJson>>(json)!.Select(band => new TariffBand(band.UpTo, band.Fixed, band.Price))]).Value;
}

internal sealed class GasTariffConverter()
    : ValueConverter<GasTariff, string>(gas => ToJson(gas), json => FromJson(json))
{
    private sealed record GasJson(decimal KilosPerCubicMeter, decimal PricePerKilo);

    private static string ToJson(GasTariff gas) => JsonSerializer.Serialize(new GasJson(gas.KilosPerCubicMeter, gas.PricePerKilo));

    private static GasTariff FromJson(string json)
    {
        GasJson gas = JsonSerializer.Deserialize<GasJson>(json)!;

        return GasTariff.Create(gas.KilosPerCubicMeter, gas.PricePerKilo).Value;
    }
}
