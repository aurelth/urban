using MediatR;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Infrastructure.Receipts;

public sealed class SendReceiptEmailJob(ITenantScope tenant, ISender sender)
{
    public Task RunAsync(Guid condominiumId, Guid paymentId, CancellationToken cancellationToken)
    {
        _ = (tenant, sender, condominiumId, paymentId, cancellationToken);

        return Task.CompletedTask;
    }
}
