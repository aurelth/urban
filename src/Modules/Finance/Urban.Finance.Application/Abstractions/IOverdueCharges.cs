using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Uma cobrança de mês fechado que passou do vencimento e ainda tem saldo, com o que já foi recebido (sem os estornados).</summary>
public sealed record OverdueRow(
    Guid ChargeId, Guid UnitId, string? Block, string Number, int Position, YearMonth Month, DateOnly DueDate, decimal Amount, decimal Paid);

/// <summary>Porta de leitura das cobranças em atraso, sempre dentro do condomínio da requisição (tenant).</summary>
public interface IOverdueCharges
{
    /// <summary>
    /// As cobranças de todos os meses fechados que venceram antes de <paramref name="today"/> e ainda não foram pagas por inteiro.
    /// É um pré-filtro: quem decide a situação é <see cref="Domain.Payments.ChargeAccount.StatusOf"/>.
    /// </summary>
    Task<IReadOnlyList<OverdueRow>> ListAsync(DateOnly today, CancellationToken cancellationToken);
}
