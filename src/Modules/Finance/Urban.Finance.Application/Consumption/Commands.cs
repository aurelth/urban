using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Consumption;

/// <summary>Uma faixa como o síndico a informa: o limite de cima em m³ (nulo na última), o valor fixo e o preço por m³.</summary>
public sealed record TariffBandInput(decimal? UpToCubicMeters, decimal FixedAmount, decimal PricePerCubicMeter);

public sealed record GasTariffInput(decimal KilosPerCubicMeter, decimal PricePerKilo);

public sealed record TariffBandView(decimal? UpToCubicMeters, decimal FixedAmount, decimal PricePerCubicMeter);

public sealed record GasTariffView(decimal KilosPerCubicMeter, decimal PricePerKilo, decimal PricePerCubicMeter);

public sealed record ConsumptionTariffView(
    string ValidFrom,
    IReadOnlyList<TariffBandView> Water,
    IReadOnlyList<TariffBandView>? Sewage,
    decimal SewagePercent,
    GasTariffView? Gas);

public sealed record ListConsumptionTariffsQuery : IRequest<Result<IReadOnlyList<ConsumptionTariffView>>>;

/// <summary>Guarda a tarifa que vale a partir do mês. Se já há uma que começa nesse mês, ela é substituída. Esgoto vazio ou nulo = não cobra esgoto.</summary>
public sealed record SaveConsumptionTariffCommand(
    string Month,
    IReadOnlyList<TariffBandInput>? Water,
    IReadOnlyList<TariffBandInput>? Sewage,
    decimal SewagePercent,
    GasTariffInput? Gas) : IRequest<Result<ConsumptionTariffView>>;

public sealed record DeleteConsumptionTariffCommand(string Month) : IRequest<Result>;
