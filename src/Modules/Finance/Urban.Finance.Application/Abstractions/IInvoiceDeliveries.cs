using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Uma cobrança de um mês fechado, com o que o envio da fatura precisa saber dela.</summary>
public sealed record ClosingCharge(Guid ChargeId, Guid UnitId, string? Block, string Number, YearMonth Month, DateOnly DueDate, decimal Amount);

/// <summary>Porta de persistência dos envios de fatura, sempre dentro do condomínio da requisição (tenant).</summary>
public interface IInvoiceDeliveries
{
    /// <returns>A cobrança, ou nulo se não existe neste condomínio.</returns>
    Task<ClosingCharge?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken);

    /// <returns>As cobranças do mês na ordem das unidades, ou nulo quando o mês não está fechado.</returns>
    Task<IReadOnlyList<ClosingCharge>?> ListChargesAsync(YearMonth month, CancellationToken cancellationToken);

    Task<InvoiceDelivery?> FindAsync(Guid chargeId, CancellationToken cancellationToken);

    /// <returns>Os envios que já existem, por id da cobrança; cobrança sem envio não aparece.</returns>
    Task<IReadOnlyDictionary<Guid, InvoiceDelivery>> ListAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken cancellationToken);

    /// <summary>Grava os envios novos de uma vez. Dois pedidos ao mesmo tempo para a mesma cobrança deixam passar um: o outro falha com <see cref="InvoiceDeliveryErrors.AlreadyQueued"/>.</summary>
    Task<Result> AddAsync(IReadOnlyCollection<InvoiceDelivery> deliveries, CancellationToken cancellationToken);

    /// <summary>Grava o que o Domain mudou num envio que já existia.</summary>
    Task SaveAsync(InvoiceDelivery delivery, CancellationToken cancellationToken);
}
