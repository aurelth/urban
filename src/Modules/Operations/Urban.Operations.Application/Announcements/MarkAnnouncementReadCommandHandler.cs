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
    public async Task<Result> Handle(MarkAnnouncementReadCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure(OperationsErrors.CondominiumNotFound);
        }

        AnnouncementRecord? record = await announcements.FindAsync(request.AnnouncementId, cancellationToken);

        if (record is null || record.Announcement.CreatedBy == request.UserId)
        {
            return Result.Failure(AnnouncementErrors.NotFound);
        }

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<ResidentUnit> units = await directory.GetUnitsAsync(request.UserId, OperationsCalendar.Today(now), cancellationToken);
        AnnouncementViewer viewer = AnnouncementRules.ViewerOf(request.UserId, units);

        return units.Count > 0 && record.Announcement.Audience.Reaches(viewer.Blocks, viewer.UnitIds)
            ? await announcements.MarkReadAsync(request.AnnouncementId, request.UserId, now, cancellationToken)
            : Result.Failure(AnnouncementErrors.NotFound);
    }
}
