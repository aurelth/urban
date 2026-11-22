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
    public Task<Result<MyAnnouncementPageView>> Handle(ListMyAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        _ = (announcements, directory, tenant, clock);
        throw new NotImplementedException();
    }
}
