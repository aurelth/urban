using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain.Announcements;

namespace Urban.Operations.Application.Announcements;

/// <param name="Recipients">Quantas contas de quem mora hoje no alcance do comunicado (sem o síndico que o escreveu).</param>
/// <param name="Readers">Quantas delas já marcaram como lido.</param>
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
    string Version);

public sealed record AnnouncementPageView(IReadOnlyList<AnnouncementView> Items, int Total, int Page, int PageSize);

public sealed record MyAnnouncementView(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool Read,
    DateTimeOffset? ReadAt)
{
    public static MyAnnouncementView From(Announcement announcement, DateTimeOffset? readAt) => new(
        announcement.Id,
        announcement.Title,
        announcement.Body,
        AnnouncementCategories.NameOf(announcement.Category),
        AnnouncementCategories.NameOf(announcement.Scope),
        announcement.CreatedAt,
        announcement.UpdatedAt,
        readAt is not null,
        readAt);
}

/// <param name="Unread">Quantos do mural a conta ainda não leu, no mural inteiro e não só nesta página.</param>
public sealed record MyAnnouncementPageView(IReadOnlyList<MyAnnouncementView> Items, int Total, int Unread, int Page, int PageSize);
