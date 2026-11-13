using Hangfire;
using Urban.Identity.Application.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

public sealed class HangfireRegistrationRequestDispatcher(IBackgroundJobClient client) : IRegistrationRequestDispatcher
{
    public Task EnqueueAsync(Guid userId, CancellationToken cancellationToken)
    {
        client.Enqueue<NotifyRegistrationRequestJob>(job => job.RunAsync(userId, CancellationToken.None));

        return Task.CompletedTask;
    }
}
