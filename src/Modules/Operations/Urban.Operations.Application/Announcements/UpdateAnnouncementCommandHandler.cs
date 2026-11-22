using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>Edita o comunicado inteiro. A versão lida na listagem precisa vir junto; quem já leu continua tendo lido.</summary>
public sealed class UpdateAnnouncementCommandHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<UpdateAnnouncementCommand, Result<AnnouncementView>>
{
    public Task<Result<AnnouncementView>> Handle(UpdateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        _ = (announcements, directory, tenant, clock);
        throw new NotImplementedException();
    }
}
