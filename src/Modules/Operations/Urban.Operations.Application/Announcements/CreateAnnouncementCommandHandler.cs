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
    public async Task<Result<AnnouncementView>> Handle(CreateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<AnnouncementView>(OperationsErrors.CondominiumNotFound);
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
        Result<Announcement> announcement = Announcement.Create(condominiumId, request.Title, request.Body, category.Value, audience.Value, request.ActorUserId, now);

        if (announcement.IsFailure)
        {
            return Result.Failure<AnnouncementView>(announcement.Error);
        }

        Result<AnnouncementAudience> checkedAudience = await AnnouncementRules.CheckAudienceAsync(audience.Value, directory, cancellationToken);

        if (checkedAudience.IsFailure)
        {
            return Result.Failure<AnnouncementView>(checkedAudience.Error);
        }

        Result<string> added = await announcements.AddAsync(announcement.Value, cancellationToken);

        if (added.IsFailure)
        {
            return Result.Failure<AnnouncementView>(added.Error);
        }

        IReadOnlyList<AnnouncementView> views = await AnnouncementRules.ViewsAsync(
            [new AnnouncementRecord(announcement.Value, added.Value)], announcements, directory, OperationsCalendar.Today(now), cancellationToken);

        return Result.Success(views[0]);
    }
}
