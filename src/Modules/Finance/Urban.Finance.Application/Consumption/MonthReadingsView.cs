using Urban.Finance.Domain.Consumption;

namespace Urban.Finance.Application.Consumption;

public sealed record MeterReadingView(decimal Previous, decimal Current, decimal Consumption)
{
    public static MeterReadingView? From(MeterReading? reading) =>
        reading is null ? null : new MeterReadingView(reading.Previous, reading.Current, reading.Consumption);
}

/// <param name="SuggestedWaterPrevious">A leitura atual de água do mês passado, para a tela sugerir como leitura anterior.</param>
/// <param name="SuggestedGasPrevious">A leitura atual de gás do mês passado.</param>
public sealed record UnitReadingsView(
    Guid UnitId,
    string? Block,
    string Number,
    MeterReadingView? Water,
    MeterReadingView? Gas,
    decimal? SuggestedWaterPrevious,
    decimal? SuggestedGasPrevious);

/// <param name="IsClosed">Verdadeiro quando o mês está fechado: as leituras não podem mais mudar.</param>
/// <param name="HasTariff">Verdadeiro quando há tarifa de consumo em vigor no mês; sem ela as leituras não entram na cobrança.</param>
/// <param name="HasGas">Verdadeiro quando a tarifa em vigor cobra gás, então toda unidade precisa de leitura de gás.</param>
public sealed record MonthReadingsView(string Month, bool IsClosed, bool HasTariff, bool HasGas, IReadOnlyList<UnitReadingsView> Units);
