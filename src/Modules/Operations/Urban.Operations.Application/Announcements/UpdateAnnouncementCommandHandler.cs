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
    public async Task<Result<AnnouncementView>> Handle(UpdateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<AnnouncementView>(OperationsErrors.CondominiumNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<AnnouncementView>(OperationsErrors.VersionRequired);
        }

        AnnouncementRecord? record = await announcements.FindAsync(request.AnnouncementId, cancellationToken);

        if (record is null)
        {
            return Result.Failure<AnnouncementView>(AnnouncementErrors.NotFound);
        }

        if (request.Version != record.Version)
        {
            return Result.Failure<AnnouncementView>(OperationsErrors.ConcurrencyConflict);
        }

        Result<AnnouncementCategory> category = AnnouncementCategories.Parse(request.Category);

        if (category.IsFailure)
        {
            return Result.Failure<AnnouncementView>(category.Error);
        }

        Result<AnnouncementAudience> audience = AnnouncementAudience.Parse(request.Scope, request.Block, request.UnitId);

        if (audience.IsFailure)
        {
            return Result.Failure<AnnouncementView>(audience.Error);
        }

        DateTimeOffset now = clock.UtcNow;
        Result edited = record.Announcement.Edit(request.Title, request.Body, category.Value, audience.Value, now);

        if (edited.IsFailure)
        {
            return Result.Failure<AnnouncementView>(edited.Error);
        }

        Result<AnnouncementAudience> checkedAudience = await AnnouncementRules.CheckAudienceAsync(audience.Value, directory, cancellationToken);

        if (checkedAudience.IsFailure)
        {
            return Result.Failure<AnnouncementView>(checkedAudience.Error);
        }

        Result<string> saved = await announcements.SaveAsync(record, cancellationToken);

        if (saved.IsFailure)
        {
            return Result.Failure<AnnouncementView>(saved.Error);
        }

        IReadOnlyList<AnnouncementView> views = await AnnouncementRules.ViewsAsync(
            [record with { Version = saved.Value }], announcements, directory, OperationsCalendar.Today(now), cancellationToken);

        return Result.Success(views[0]);
    }
}
