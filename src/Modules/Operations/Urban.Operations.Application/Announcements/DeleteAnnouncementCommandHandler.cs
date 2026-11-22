using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Application.Announcements;

/// <summary>Apaga o comunicado e as leituras dele. A versão lida na listagem precisa vir junto.</summary>
public sealed class DeleteAnnouncementCommandHandler(IAnnouncements announcements, ITenantContext tenant) : IRequestHandler<DeleteAnnouncementCommand, Result>
{
    public Task<Result> Handle(DeleteAnnouncementCommand request, CancellationToken cancellationToken)
    {
        _ = (announcements, tenant);
        throw new NotImplementedException();
    }
}
