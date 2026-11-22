using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

public sealed class ListAnnouncementsQueryHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<ListAnnouncementsQuery, Result<AnnouncementPageView>>
{
    public async Task<Result<AnnouncementPageView>> Handle(ListAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<AnnouncementPageView>(OperationsErrors.CondominiumNotFound);
        }

        if (!AnnouncementRules.IsValidPage(request.Page, request.PageSize))
        {
            return Result.Failure<AnnouncementPageView>(AnnouncementErrors.PageInvalid);
        }

        PagedAnnouncements page = await announcements.ListAsync(request.Page, request.PageSize, cancellationToken);
        IReadOnlyList<AnnouncementView> views = await AnnouncementRules.ViewsAsync(page.Items, announcements, directory, OperationsCalendar.Today(clock.UtcNow), cancellationToken);

        return Result.Success(new AnnouncementPageView(views, page.Total, request.Page, request.PageSize));
    }
}
