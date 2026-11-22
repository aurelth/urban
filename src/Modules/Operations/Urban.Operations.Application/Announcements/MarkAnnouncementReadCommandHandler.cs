using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// Marca o comunicado como lido. Só quem o recebe pode marcar (quem mora no alcance dele): um comunicado de outro bloco, de outra unidade,
/// do próprio síndico ou de quem não mora em unidade nenhuma responde como se não existisse, para a resposta não revelar o que a conta não vê.
/// </summary>
public sealed class MarkAnnouncementReadCommandHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<MarkAnnouncementReadCommand, Result>
{
    public Task<Result> Handle(MarkAnnouncementReadCommand request, CancellationToken cancellationToken)
    {
        _ = (announcements, directory, tenant, clock);
        throw new NotImplementedException();
    }
}
