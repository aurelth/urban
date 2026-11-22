using MediatR;
using Urban.SharedKernel;

namespace Urban.Operations.Application.Announcements;

/// <summary>Cria um comunicado. <paramref name="Scope"/> é <c>condominium</c>, <c>block</c> ou <c>unit</c>; o bloco só vale no alcance de bloco e a unidade, no de unidade.</summary>
public sealed record CreateAnnouncementCommand(Guid ActorUserId, string? Title, string? Body, string? Category, string? Scope, string? Block, Guid? UnitId)
    : IRequest<Result<AnnouncementView>>;

public sealed record UpdateAnnouncementCommand(Guid AnnouncementId, string? Version, string? Title, string? Body, string? Category, string? Scope, string? Block, Guid? UnitId)
    : IRequest<Result<AnnouncementView>>;

public sealed record DeleteAnnouncementCommand(Guid AnnouncementId, string? Version) : IRequest<Result>;

/// <summary>Os comunicados do condomínio, do mais novo ao mais velho, cada um com quantos já leram.</summary>
public sealed record ListAnnouncementsQuery(int Page, int PageSize) : IRequest<Result<AnnouncementPageView>>;

/// <summary>
/// O mural de quem mora: os comunicados que chegam à conta, com o que ela já leu e quantos faltam. <paramref name="Category"/> em branco
/// mostra todas as categorias; um nome que não é categoria falha.
/// </summary>
public sealed record ListMyAnnouncementsQuery(Guid UserId, bool UnreadOnly, int Page, int PageSize, string? Category = null) : IRequest<Result<MyAnnouncementPageView>>;

public sealed record MarkAnnouncementReadCommand(Guid UserId, Guid AnnouncementId) : IRequest<Result>;

/// <summary>Marca como lidos todos os comunicados do mural da conta (só os da categoria, se informada). Devolve quantos foram marcados agora.</summary>
public sealed record MarkAllAnnouncementsReadCommand(Guid UserId, string? Category = null) : IRequest<Result<int>>;
