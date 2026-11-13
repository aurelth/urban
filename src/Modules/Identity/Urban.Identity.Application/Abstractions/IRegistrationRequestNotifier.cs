using Urban.Identity.Application.RegistrationRequests;

namespace Urban.Identity.Application.Abstractions;

public interface IRegistrationRequestNotifier
{
    Task NotifyAsync(RegistrationRequestSummary request, CancellationToken cancellationToken);
}
