using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Porta de persistência das tarifas de consumo, sempre dentro do condomínio da requisição.</summary>
public interface IConsumptionTariffs
{
    /// <summary>Todas as tarifas do condomínio, da que começou mais recentemente para a mais antiga.</summary>
    Task<IReadOnlyList<ConsumptionTariff>> ListAsync(CancellationToken cancellationToken);

    Task<ConsumptionTariff?> FindAsync(YearMonth validFrom, CancellationToken cancellationToken);

    /// <summary>Grava a tarifa. Se já existe uma que começa no mesmo mês, ela é trocada por esta, na mesma operação.</summary>
    Task<Result> SaveAsync(ConsumptionTariff tariff, CancellationToken cancellationToken);

    Task RemoveAsync(ConsumptionTariff tariff, CancellationToken cancellationToken);
}
