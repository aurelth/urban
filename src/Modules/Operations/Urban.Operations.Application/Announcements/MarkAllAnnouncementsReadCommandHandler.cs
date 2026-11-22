using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// Marca como lidos todos os comunicados do mural da conta, os que ela recebe pelo lugar onde mora hoje (só os de uma categoria, se informada).
/// Quem não mora em unidade nenhuma não recebe nada e, por isso, não marca nada. Devolve quantos foram marcados agora.
/// </summary>
public sealed class MarkAllAnnouncementsReadCommandHandler(
    IAnnouncements announcements,
    IAnnouncementAudience directory,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<MarkAllAnnouncementsReadCommand, Result<int>>
{
    public async Task<Result<int>> Handle(MarkAllAnnouncementsReadCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<int>(OperationsErrors.CondominiumNotFound);
        }

        Result<AnnouncementCategory?> category = AnnouncementRules.ParseCategoryFilter(request.Category);

        if (category.IsFailure)
        {
            return Result.Failure<int>(category.Error);
        }

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<ResidentUnit> units = await directory.GetUnitsAsync(request.UserId, OperationsCalendar.Today(now), cancellationToken);

        if (units.Count == 0)
        {
            return Result.Success(0);
        }

        int marked = await announcements.MarkAllReadAsync(AnnouncementRules.ViewerOf(request.UserId, units), category.Value, now, cancellationToken);

        return Result.Success(marked);
    }
}
