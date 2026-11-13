using Hangfire;
using Urban.Identity.Application.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

public sealed class HangfireContactMessageDispatcher(IBackgroundJobClient client) : IContactMessageDispatcher
{
    public Task EnqueueAsync(Guid messageId, CancellationToken cancellationToken)
    {
        client.Enqueue<NotifyContactMessageJob>(job => job.RunAsync(messageId, CancellationToken.None));

        return Task.CompletedTask;
    }
}
