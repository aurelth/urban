using Microsoft.AspNetCore.SignalR;
using Urban.Operations.Application.Abstractions;

namespace Urban.Api.Hubs;

/// <summary>Leva o aviso do mural às conexões abertas de cada conta, pelo SignalR.</summary>
public sealed class SignalRAnnouncementEvents(IHubContext<AnnouncementsHub> hub) : IAnnouncementEvents
{
    public Task SendAsync(AnnouncementChange change, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hub);

        throw new NotImplementedException();
    }
}
