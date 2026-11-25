using Hangfire;
using Urban.Finance.Application.Abstractions;

namespace Urban.Finance.Infrastructure.Receipts;

/// <summary>Enfileira o envio do recibo no Hangfire. É uma gravação rápida no banco da fila; o envio de verdade acontece fora da requisição.</summary>
public sealed class HangfireReceiptEmailDispatcher(IBackgroundJobClient client) : IReceiptEmailDispatcher
{
    public Task EnqueueAsync(Guid condominiumId, Guid paymentId, CancellationToken cancellationToken)
    {
        client.Enqueue<SendReceiptEmailJob>(job => job.RunAsync(condominiumId, paymentId, CancellationToken.None));

        return Task.CompletedTask;
    }
}
