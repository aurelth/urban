namespace Urban.Operations.Application.Abstractions;

/// <summary>O que mudou num comunicado, do ponto de vista de quem está com o mural aberto.</summary>
public enum AnnouncementChangeKind
{
    Published,
    Updated,
    Removed,
}

/// <summary>O aviso em tempo real: só o que mudou e qual comunicado, sem título nem texto (a tela busca o resto pela API, que confere o alcance).</summary>
public sealed record AnnouncementChange(AnnouncementChangeKind Kind, Guid AnnouncementId);

/// <summary>
/// Porta que leva ao navegador de cada conta o aviso de que o mural mudou. Quem hospeda a API a cumpre (SignalR); as Operações só dizem
/// quem deve saber. Entregar é melhor esforço: quem não estava conectado vê a mudança na próxima vez que abrir o mural.
/// </summary>
public interface IAnnouncementEvents
{
    Task SendAsync(AnnouncementChange change, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
