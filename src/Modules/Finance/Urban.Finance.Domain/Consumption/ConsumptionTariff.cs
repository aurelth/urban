using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Consumption;

/// <summary>
/// As tarifas de consumo que valem a partir de um mês: água por faixas, esgoto por faixas (opcional) com o percentual
/// cobrado dele, e gás (opcional). Um reajuste é uma tarifa nova com outro mês de início; as antigas ficam, e cada mês fechado
/// usa a que valia nele.
/// </summary>
public sealed class ConsumptionTariff
{
    private ConsumptionTariff(Guid condominiumId, YearMonth validFrom, TariffScale water, TariffScale? sewage, decimal sewagePercent, GasTariff? gas, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        ValidFrom = validFrom;
        Water = water;
        Sewage = sewage;
        SewagePercent = sewagePercent;
        Gas = gas;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private ConsumptionTariff()
    {
        Water = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public YearMonth ValidFrom { get; }

    public TariffScale Water { get; }

    public TariffScale? Sewage { get; }

    /// <summary>Quanto das faixas do esgoto se cobra: 100 normalmente, 50 na tarifa parcial de quem não tem a rede completa.</summary>
    public decimal SewagePercent { get; }

    public GasTariff? Gas { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Result<ConsumptionTariff> Create(Guid condominiumId, YearMonth validFrom, TariffScale water, TariffScale? sewage, decimal sewagePercent, GasTariff? gas, DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<ConsumptionTariff>(ConsumptionErrors.CondominiumRequired);
        }

        return ReserveFund.IsValidPercent(sewagePercent)
            ? Result.Success(new ConsumptionTariff(condominiumId, validFrom, water, sewage, sewagePercent, gas, now))
            : Result.Failure<ConsumptionTariff>(ConsumptionErrors.SewagePercentInvalid);
    }

    /// <summary>A tarifa que vale no mês: a mais recente que já tinha começado. Nula antes da primeira.</summary>
    public static ConsumptionTariff? CurrentFor(YearMonth month, IEnumerable<ConsumptionTariff> tariffs) =>
        tariffs.Where(tariff => tariff.ValidFrom <= month).MaxBy(tariff => tariff.ValidFrom);

    public decimal WaterCharge(decimal cubicMeters) => Water.ChargeFor(cubicMeters);

    public decimal SewageCharge(decimal cubicMeters) =>
        Sewage is null ? 0m : MoneyRules.Round(Sewage.RawChargeFor(cubicMeters) * SewagePercent / 100m);

    public decimal GasCharge(decimal cubicMeters) => Gas?.ChargeFor(cubicMeters) ?? 0m;
}
