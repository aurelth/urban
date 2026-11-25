using Hangfire;
using Urban.Finance.Application.Abstractions;

namespace Urban.Finance.Infrastructure.Receipts;

public sealed class HangfireReceiptEmailDispatcher(IBackgroundJobClient client) : IReceiptEmailDispatcher
{
    public Task EnqueueAsync(Guid condominiumId, Guid paymentId, CancellationToken cancellationToken)
    {
        _ = (client, condominiumId, paymentId);

        return Task.CompletedTask;
    }
}
