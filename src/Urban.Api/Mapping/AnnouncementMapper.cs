using Urban.Api.Endpoints;
using Urban.Operations.Application.Announcements;

namespace Urban.Api.Mapping;

public static class AnnouncementMapper
{
    public static AnnouncementResponse ToResponse(AnnouncementView view) => new(
        view.Id,
        view.Title,
        view.Body,
        view.Category,
        view.Scope,
        view.Block,
        view.UnitId,
        view.CreatedAt,
        view.UpdatedAt,
        view.Recipients,
        view.Readers,
        view.Version);

    public static AnnouncementPageResponse ToResponse(AnnouncementPageView view) =>
        new([.. view.Items.Select(ToResponse)], view.Total, view.Page, view.PageSize);

    public static MyAnnouncementResponse ToResponse(MyAnnouncementView view) => new(
        view.Id,
        view.Title,
        view.Body,
        view.Category,
        view.Scope,
        view.CreatedAt,
        view.UpdatedAt,
        view.Read,
        view.ReadAt);

    public static MyAnnouncementPageResponse ToResponse(MyAnnouncementPageView view) =>
        new([.. view.Items.Select(ToResponse)], view.Total, view.Unread, view.Page, view.PageSize);
}
