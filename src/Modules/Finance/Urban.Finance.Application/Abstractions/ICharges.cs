using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Abstractions;

/// <summary>O total de uma linha da fatura somado em todas as cobranças do fechamento: é o que o condomínio gastou naquela linha.</summary>
public sealed record ClosingLineTotal(ChargeLineKind Kind, string Label, decimal Amount);

/// <param name="Charge">A cobrança com todas as linhas, como foi emitida no fechamento.</param>
/// <param name="Month">Competência do fechamento.</param>
/// <param name="DueDate">Vencimento escolhido por quem fechou.</param>
/// <param name="ClosedAt">Quando o mês foi fechado.</param>
/// <param name="BasisKind">Em que o rateio se baseou naquele fechamento.</param>
/// <param name="TotalWeight">A soma dos pesos de todas as cobranças do fechamento; o peso da cobrança dividido por ela é a participação da unidade.</param>
/// <param name="LineTotals">O total de cada linha da fatura no fechamento todo (grupos, fundo de reserva e consumo), na ordem da fatura.</param>
public sealed record ChargeDetail(
    UnitCharge Charge,
    YearMonth Month,
    DateOnly DueDate,
    DateTimeOffset ClosedAt,
    ApportionmentBasisKind BasisKind,
    decimal TotalWeight,
    IReadOnlyList<ClosingLineTotal> LineTotals);

public interface ICharges
{
    /// <returns>A cobrança do condomínio da requisição com as linhas; nulo se não existe ou é de outro condomínio.</returns>
    Task<ChargeDetail?> FindAsync(Guid chargeId, CancellationToken cancellationToken);
}
