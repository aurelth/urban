using Hangfire;
using Urban.Finance.Application.Abstractions;

namespace Urban.Finance.Infrastructure.Invoices;

/// <summary>Enfileira o envio da fatura no Hangfire. É uma gravação rápida no banco da fila; o envio de verdade acontece fora da requisição.</summary>
public sealed class HangfireInvoiceDeliveryDispatcher(IBackgroundJobClient client) : IInvoiceDeliveryDispatcher
{
    public Task EnqueueAsync(Guid condominiumId, Guid chargeId, CancellationToken cancellationToken)
    {
        client.Enqueue<SendInvoiceJob>(job => job.RunAsync(condominiumId, chargeId, CancellationToken.None));

        return Task.CompletedTask;
    }
}
