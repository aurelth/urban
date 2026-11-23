using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Urban.Api.Hubs;

/// <summary>
/// O canal em que o mural avisa o navegador de que algo mudou. Só o servidor fala: o navegador abre a conexão com o token de acesso e
/// escuta <see cref="ChangedMethod"/>; nada vindo do navegador é lido.
/// </summary>
[Authorize]
public sealed class AnnouncementsHub : Hub
{
    public const string Path = RealtimeExtensions.HubsPrefix + "/announcements";

    public const string ChangedMethod = "announcementChanged";
}
