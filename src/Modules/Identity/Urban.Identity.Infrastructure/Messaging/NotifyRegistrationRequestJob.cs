using Hangfire;
using MediatR;
using Urban.Identity.Application.RegistrationRequests.NotifyRegistrationRequest;

namespace Urban.Identity.Infrastructure.Messaging;

public sealed class NotifyRegistrationRequestJob(ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        await sender.Send(new NotifyRegistrationRequestCommand(userId), cancellationToken);
    }
}
