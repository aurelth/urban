using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Abstractions;

/// <summary>O total de uma linha do recibo somado em todas as cobranças do fechamento: é o que o condomínio gastou naquela linha.</summary>
public sealed record ClosingLineTotal(ChargeLineKind Kind, string Label, decimal Amount);

/// <param name="Charge">A cobrança com todas as linhas, como foi emitida no fechamento.</param>
/// <param name="Month">Competência do fechamento.</param>
/// <param name="DueDate">Vencimento escolhido por quem fechou.</param>
/// <param name="ClosedAt">Quando o mês foi fechado.</param>
/// <param name="BasisKind">Em que o rateio se baseou naquele fechamento.</param>
/// <param name="TotalWeight">A soma dos pesos de todas as cobranças do fechamento; o peso da cobrança dividido por ela é a participação da unidade.</param>
/// <param name="LineTotals">O total de cada linha do recibo no fechamento todo (grupos, fundo de reserva e consumo), na ordem do recibo.</param>
public sealed record ChargeDetail(
    UnitCharge Charge,
    YearMonth Month,
    DateOnly DueDate,
    DateTimeOffset ClosedAt,
    ApportionmentBasisKind BasisKind,
    decimal TotalWeight,
    IReadOnlyList<ClosingLineTotal> LineTotals);

/// <summary>Uma cobrança antiga da unidade e quanto dela já foi pago (pagamentos estornados não contam).</summary>
public sealed record EarlierCharge(Guid ChargeId, YearMonth Month, DateOnly DueDate, decimal Amount, decimal Paid);

public interface ICharges
{
    /// <returns>A cobrança do condomínio da requisição com as linhas; nulo se não existe ou é de outro condomínio.</returns>
    Task<ChargeDetail?> FindAsync(Guid chargeId, CancellationToken cancellationToken);

    /// <summary>
    /// As cobranças da unidade de meses anteriores a <paramref name="before"/> que ainda têm saldo, da mais antiga para a mais
    /// nova, no máximo <paramref name="limit"/>. Uma consulta só, com os pagamentos somados no banco.
    /// </summary>
    Task<IReadOnlyList<EarlierCharge>> ListEarlierOpenAsync(Guid unitId, YearMonth before, int limit, CancellationToken cancellationToken);
}
