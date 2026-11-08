namespace Urban.Finance.Domain.Consumption;

/// <summary>
/// Uma faixa da tarifa progressiva. <paramref name="UpToCubicMeters"/> é o limite de cima da faixa (nulo na última).
/// <paramref name="FixedAmount"/> é cobrado uma vez quando o consumo chega à faixa (na primeira, sempre: é o mínimo);
/// <paramref name="PricePerCubicMeter"/> vale para os m³ que caem dentro dela.
/// </summary>
public sealed record TariffBand(decimal? UpToCubicMeters, decimal FixedAmount, decimal PricePerCubicMeter);
