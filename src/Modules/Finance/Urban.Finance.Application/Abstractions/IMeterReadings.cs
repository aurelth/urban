using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Porta de persistência das leituras dos medidores, sempre dentro do condomínio da requisição.</summary>
public interface IMeterReadings
{
    /// <summary>As leituras do mês (todas as unidades, água e gás). As devolvidas ficam rastreadas: corrigir uma e chamar <see cref="SaveAsync"/> grava a correção.</summary>
    Task<IReadOnlyList<MeterReading>> ListAsync(YearMonth month, CancellationToken cancellationToken);

    /// <summary>Grava as leituras novas, as corrigidas e a retirada das removidas numa única operação.</summary>
    Task<Result> SaveAsync(IReadOnlyList<MeterReading> added, IReadOnlyList<MeterReading> removed, CancellationToken cancellationToken);
}
