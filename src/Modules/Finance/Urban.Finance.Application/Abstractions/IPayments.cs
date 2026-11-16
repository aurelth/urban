using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>A cobrança de uma unidade num mês fechado, com o que o pagamento precisa saber dela.</summary>
public sealed record ChargeRef(Guid ChargeId, Guid ClosingId, YearMonth Month, DateOnly DueDate, decimal Amount);

/// <summary>Uma cobrança do mês com o que já foi recebido (sem os estornados) e a data do último recebimento.</summary>
public sealed record ChargeRow(Guid ChargeId, Guid UnitId, string? Block, string Number, decimal Amount, decimal Paid, DateOnly? LastReceivedOn);

/// <summary>
/// Uma página das cobranças do mês. Os totais valem para o mês inteiro e <paramref name="Total"/> conta as cobranças que
/// passam pelo filtro de situação, para a paginação.
/// </summary>
public sealed record MonthPaymentsPage(DateOnly DueDate, decimal Charged, decimal Received, int Total, IReadOnlyList<ChargeRow> Items);

/// <summary>Porta de persistência dos pagamentos, sempre dentro do condomínio da requisição (tenant).</summary>
public interface IPayments
{
    /// <returns>A cobrança, ou nulo se não existe neste condomínio.</returns>
    Task<ChargeRef?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken);

    /// <summary>Todos os pagamentos da cobrança, os estornados também, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<Payment>> ListForChargeAsync(Guid chargeId, CancellationToken cancellationToken);

    Task<Payment?> FindAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Grava o pagamento depois de conferir de novo, com a cobrança travada, que ele cabe no saldo: dois pagamentos ao mesmo tempo
    /// não passam do valor. Falha com <see cref="PaymentErrors.ExceedsBalance"/>.
    /// </summary>
    Task<Result> AddAsync(Payment payment, decimal chargeAmount, CancellationToken cancellationToken);

    /// <summary>
    /// Grava o estorno que o Domain já marcou no pagamento, só se ele ainda não estava estornado no banco; dois estornos ao mesmo
    /// tempo deixam passar o primeiro e o segundo falha com <see cref="PaymentErrors.AlreadyReversed"/>.
    /// </summary>
    Task<Result> ReverseAsync(Payment payment, CancellationToken cancellationToken);

    /// <summary>Se alguma cobrança do fechamento tem pagamento, estornado ou não.</summary>
    Task<bool> AnyForClosingAsync(Guid closingId, CancellationToken cancellationToken);

    /// <returns>A página, ou nulo quando o mês não está fechado.</returns>
    Task<MonthPaymentsPage?> ListMonthAsync(
        YearMonth month, ChargeStatus? status, DateOnly today, int page, int pageSize, CancellationToken cancellationToken);
}
