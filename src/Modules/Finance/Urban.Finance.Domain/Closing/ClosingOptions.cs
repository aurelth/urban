using Urban.Finance.Domain.Consumption;

namespace Urban.Finance.Domain.Closing;

/// <summary>
/// O que o condomínio configurou e vale para este fechamento: os nomes dos grupos de despesa (cada um vira uma linha do recibo),
/// quais grupos formam a base do fundo de reserva e o percentual dele, a tarifa de consumo do mês (nula se não há) e as leituras.
/// </summary>
public sealed record ClosingOptions(
    IReadOnlyDictionary<Guid, string> GroupNames,
    IReadOnlySet<Guid> ReserveFundGroupIds,
    decimal ReserveFundPercent,
    ConsumptionTariff? Tariff,
    IReadOnlyList<MeterReading> Readings);
