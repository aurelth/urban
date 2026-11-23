using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// Descobre quem recebe o e-mail do comunicado e abre um envio por conta. Uma consulta ao condomínio diz quem mora hoje no alcance; o
/// síndico que escreveu fica de fora. Comunicado que não pediu e-mail, ou que já foi apagado, não abre envio nenhum.
/// </summary>
public sealed class FanOutAnnouncementEmailsCommandHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    IAnnouncementMailDispatcher dispatcher,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<FanOutAnnouncementEmailsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(FanOutAnnouncementEmailsCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<int>(OperationsErrors.CondominiumNotFound);
        }

        AnnouncementRecord? record = await announcements.FindAsync(request.AnnouncementId, cancellationToken);

        if (record is null || !record.Announcement.NotifyByEmail)
        {
            return Result.Success(0);
        }

        Domain.Announcements.Announcement announcement = record.Announcement;
        IReadOnlyList<IReadOnlySet<Guid>> recipients = await directory.GetRecipientsAsync(
            [new AudienceTarget(announcement.Block, announcement.UnitId)], OperationsCalendar.Today(clock.UtcNow), cancellationToken);
        Guid[] accounts = [.. recipients[0].Where(userId => userId != announcement.CreatedBy)];

        foreach (Guid userId in accounts)
        {
            await dispatcher.EnqueueDeliveryAsync(condominiumId, announcement.Id, userId, cancellationToken);
        }

        return Result.Success(accounts.Length);
    }
}
