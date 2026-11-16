using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Abstractions;

/// <param name="Charge">A cobrança com todas as linhas, como foi emitida no fechamento.</param>
/// <param name="Month">Competência do fechamento.</param>
/// <param name="DueDate">Vencimento escolhido por quem fechou.</param>
/// <param name="ClosedAt">Quando o mês foi fechado.</param>
public sealed record ChargeDetail(UnitCharge Charge, YearMonth Month, DateOnly DueDate, DateTimeOffset ClosedAt);

public interface ICharges
{
    /// <returns>A cobrança do condomínio da requisição com as linhas; nulo se não existe ou é de outro condomínio.</returns>
    Task<ChargeDetail?> FindAsync(Guid chargeId, CancellationToken cancellationToken);
}
