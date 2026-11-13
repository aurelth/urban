using Hangfire;
using MediatR;
using Urban.Identity.Application.Contact.NotifyContactMessage;

namespace Urban.Identity.Infrastructure.Messaging;

public sealed class NotifyContactMessageJob(ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await sender.Send(new NotifyContactMessageCommand(messageId), cancellationToken);
    }
}
