using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// Cria o comunicado. A categoria, o alcance e o texto são conferidos antes de qualquer consulta; só então o bloco ou a unidade escolhidos
/// são conferidos com o condomínio (um bloco sem unidades ou uma unidade de outro condomínio não vira comunicado que ninguém recebe).
/// </summary>
public sealed class CreateAnnouncementCommandHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<CreateAnnouncementCommand, Result<AnnouncementView>>
{
    public Task<Result<AnnouncementView>> Handle(CreateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        _ = (announcements, directory, tenant, clock);
        throw new NotImplementedException();
    }
}
