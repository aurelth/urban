using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>Descobre quem recebe o e-mail do comunicado e abre um envio por conta.</summary>
public sealed class FanOutAnnouncementEmailsCommandHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    IAnnouncementMailDispatcher dispatcher,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<FanOutAnnouncementEmailsCommand, Result<int>>
{
    public Task<Result<int>> Handle(FanOutAnnouncementEmailsCommand request, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"Não implementado: {request.AnnouncementId} {announcements.GetType().Name} {directory.GetType().Name} {dispatcher.GetType().Name} {tenant.CondominiumId} {clock.UtcNow}");
}
