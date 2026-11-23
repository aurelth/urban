using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <param name="Recipients">Quantas contas de quem mora hoje no alcance do comunicado (sem o síndico que o escreveu).</param>
/// <param name="Readers">Quantas delas já marcaram como lido.</param>
/// <param name="NotifiedByEmail">Se o síndico pediu o e-mail ao publicar.</param>
public sealed record AnnouncementView(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string Scope,
    string? Block,
    Guid? UnitId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Recipients,
    int Readers,
    bool NotifiedByEmail,
    string Version);

public sealed record AnnouncementPageView(IReadOnlyList<AnnouncementView> Items, int Total, int Page, int PageSize);

/// <summary>A unidade de um comunicado de alcance de unidade, como a conta que lê a conhece (a dela).</summary>
public sealed record MyAnnouncementUnitView(Guid Id, string? Block, string Number);

/// <param name="Block">O bloco de um comunicado de alcance de bloco; nos outros alcances, nada.</param>
/// <param name="Unit">A unidade de um comunicado de alcance de unidade; nos outros alcances, nada.</param>
public sealed record MyAnnouncementView(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string Scope,
    string? Block,
    MyAnnouncementUnitView? Unit,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool Read,
    DateTimeOffset? ReadAt)
{
    public static MyAnnouncementView From(Announcement announcement, DateTimeOffset? readAt, IReadOnlyList<ResidentUnit> units) => new(
        announcement.Id,
        announcement.Title,
        announcement.Body,
        AnnouncementCategories.NameOf(announcement.Category),
        AnnouncementCategories.NameOf(announcement.Scope),
        announcement.Scope == AnnouncementScope.Block ? announcement.Block : null,
        UnitOf(announcement, units),
        announcement.CreatedAt,
        announcement.UpdatedAt,
        readAt is not null,
        readAt);

    /// <summary>A unidade vem de onde a conta mora (quem lê só vê comunicado de unidade que é dela), sem consultar o condomínio de novo.</summary>
    private static MyAnnouncementUnitView? UnitOf(Announcement announcement, IReadOnlyList<ResidentUnit> units) =>
        announcement.Scope == AnnouncementScope.Unit && units.FirstOrDefault(unit => unit.UnitId == announcement.UnitId) is { } found
            ? new MyAnnouncementUnitView(found.UnitId, found.Block, found.Number)
            : null;
}

/// <param name="UnreadByCategory">Quantos do mural a conta ainda não leu em cada categoria, no mural inteiro: nem a página nem os filtros mudam a conta.</param>
public sealed record MyAnnouncementPageView(IReadOnlyList<MyAnnouncementView> Items, int Total, UnreadCounts UnreadByCategory, int Page, int PageSize)
{
    /// <summary>Quantos do mural a conta ainda não leu, somando as categorias.</summary>
    public int Unread => UnreadByCategory.Total;
}
