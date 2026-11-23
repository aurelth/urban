using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// O envio de verdade do comunicado a uma conta, no segundo plano. Comunicado apagado nesse meio tempo, que não pediu e-mail ou conta que
/// já não pode receber (desativada, suspensa, de outro condomínio) terminam sem enviar e sem erro: repetir não adiantaria. Só a falha do
/// provedor sobe, para a fila tentar de novo.
/// </summary>
public sealed class SendAnnouncementEmailCommandHandler(
    IAnnouncements announcements,
    IAccountEmails accounts,
    IAnnouncementLinks links,
    IEmailSender email,
    ITenantContext tenant) : IRequestHandler<SendAnnouncementEmailCommand, Result>
{
    public async Task<Result> Handle(SendAnnouncementEmailCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure(OperationsErrors.CondominiumNotFound);
        }

        AnnouncementRecord? record = await announcements.FindAsync(request.AnnouncementId, cancellationToken);

        if (record is null || !record.Announcement.NotifyByEmail)
        {
            return Result.Success();
        }

        string? address = await accounts.FindActiveEmailAsync(request.UserId, condominiumId, cancellationToken);

        if (address is null)
        {
            return Result.Success();
        }

        await email.SendAsync(AnnouncementEmail.Build(record.Announcement, address, links.GetMuralUri()), cancellationToken);

        return Result.Success();
    }
}
