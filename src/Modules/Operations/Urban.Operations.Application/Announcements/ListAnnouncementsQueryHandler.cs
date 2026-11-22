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
    public Task<Result<AnnouncementPageView>> Handle(ListAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        _ = (announcements, directory, tenant, clock);
        throw new NotImplementedException();
    }
}
