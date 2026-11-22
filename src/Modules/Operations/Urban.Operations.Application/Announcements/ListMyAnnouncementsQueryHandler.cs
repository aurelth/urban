using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// O mural de quem mora. O alcance vem de onde a conta mora hoje (o vínculo em andamento, de qualquer papel), nunca de dado da requisição.
/// Quem não mora em unidade nenhuma não vê comunicado algum.
/// </summary>
public sealed class ListMyAnnouncementsQueryHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<ListMyAnnouncementsQuery, Result<MyAnnouncementPageView>>
{
    public async Task<Result<MyAnnouncementPageView>> Handle(ListMyAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MyAnnouncementPageView>(OperationsErrors.CondominiumNotFound);
        }

        if (!AnnouncementRules.IsValidPage(request.Page, request.PageSize))
        {
            return Result.Failure<MyAnnouncementPageView>(AnnouncementErrors.PageInvalid);
        }

        IReadOnlyList<ResidentUnit> units = await directory.GetUnitsAsync(request.UserId, OperationsCalendar.Today(clock.UtcNow), cancellationToken);

        if (units.Count == 0)
        {
            return Result.Success(new MyAnnouncementPageView([], 0, UnreadCounts.None, request.Page, request.PageSize));
        }

        AnnouncementViewer viewer = AnnouncementRules.ViewerOf(request.UserId, units);
        PagedAnnouncements page = await announcements.ListVisibleAsync(viewer, new MuralFilter(request.UnreadOnly), request.Page, request.PageSize, cancellationToken);
        UnreadCounts unread = await announcements.CountUnreadAsync(viewer, cancellationToken);
        IReadOnlyDictionary<Guid, DateTimeOffset> readAt = page.Items.Count == 0
            ? new Dictionary<Guid, DateTimeOffset>()
            : await announcements.ListReadAtAsync(request.UserId, [.. page.Items.Select(record => record.Announcement.Id)], cancellationToken);

        return Result.Success(new MyAnnouncementPageView(
            [.. page.Items.Select(record => MyAnnouncementView.From(record.Announcement, readAt.TryGetValue(record.Announcement.Id, out DateTimeOffset at) ? at : null, units))],
            page.Total,
            unread,
            request.Page,
            request.PageSize));
    }
}
