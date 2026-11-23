using Microsoft.AspNetCore.SignalR;
using Urban.Operations.Application.Abstractions;

namespace Urban.Api.Hubs;

/// <summary>Leva o aviso do mural às conexões abertas de cada conta, pelo SignalR.</summary>
public sealed class SignalRAnnouncementEvents(IHubContext<AnnouncementsHub> hub) : IAnnouncementEvents
{
    public Task SendAsync(AnnouncementChange change, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Só o tipo e o id vão pela conexão: título e texto a tela busca pela API, que confere o alcance de quem pergunta.
        AnnouncementChangeMessage message = new(KindName(change.Kind), change.AnnouncementId);

        return hub.Clients.Users(userIds.Select(id => id.ToString())).SendAsync(AnnouncementsHub.ChangedMethod, message, cancellationToken);
    }

    internal static string KindName(AnnouncementChangeKind kind) => kind switch
    {
        AnnouncementChangeKind.Published => "published",
        AnnouncementChangeKind.Updated => "updated",
        AnnouncementChangeKind.Removed => "removed",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private sealed record AnnouncementChangeMessage(string Kind, Guid Id);
}
