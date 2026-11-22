using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>O que os casos de uso de comunicado repetem: conferir página e alcance, e montar a visão do síndico com a contagem de leitores.</summary>
internal static class AnnouncementRules
{
    public const int MaxPageSize = 100;

    public static bool IsValidPage(int page, int pageSize) => page >= 1 && pageSize is >= 1 and <= MaxPageSize;

    /// <summary>A categoria do filtro do mural: em branco é "todas" (sem filtro) e um nome que não é categoria falha.</summary>
    public static Result<AnnouncementCategory?> ParseCategoryFilter(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Success<AnnouncementCategory?>(null);
        }

        Result<AnnouncementCategory> parsed = AnnouncementCategories.Parse(name);

        return parsed.IsSuccess ? Result.Success<AnnouncementCategory?>(parsed.Value) : Result.Failure<AnnouncementCategory?>(parsed.Error);
    }

    /// <summary>O alcance já montado, conferido com o condomínio: o bloco precisa ter unidades e a unidade precisa existir nele.</summary>
    public static async Task<Result<AnnouncementAudience>> CheckAudienceAsync(AnnouncementAudience audience, IAnnouncementAudience directory, CancellationToken cancellationToken)
    {
        if (audience.Block is { } block && !await directory.BlockExistsAsync(block, cancellationToken))
        {
            return Result.Failure<AnnouncementAudience>(AnnouncementErrors.BlockNotFound);
        }

        if (audience.UnitId is { } unitId && await directory.FindUnitAsync(unitId, cancellationToken) is null)
        {
            return Result.Failure<AnnouncementAudience>(AnnouncementErrors.UnitNotFound);
        }

        return Result.Success(audience);
    }

    /// <summary>
    /// A visão do síndico de cada comunicado, com quantas contas moram hoje no alcance e quantas delas leram. Uma consulta de moradores e
    /// uma de leituras para todos os comunicados, nunca uma por comunicado. Quem leu e já não mora no alcance não conta, e o síndico que
    /// escreveu também não.
    /// </summary>
    public static async Task<IReadOnlyList<AnnouncementView>> ViewsAsync(
        IReadOnlyList<AnnouncementRecord> records,
        IAnnouncements announcements,
        IAnnouncementAudience directory,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await directory.GetRecipientsAsync(
            [.. records.Select(record => new AudienceTarget(record.Announcement.Block, record.Announcement.UnitId))], today, cancellationToken);
        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> readers = await announcements.ListReadersAsync([.. records.Select(record => record.Announcement.Id)], cancellationToken);

        List<AnnouncementView> views = [];

        for (int i = 0; i < records.Count; i++)
        {
            Announcement announcement = records[i].Announcement;
            HashSet<Guid> audience = [.. recipients[i]];
            audience.Remove(announcement.CreatedBy);
            IReadOnlySet<Guid> read = readers.GetValueOrDefault(announcement.Id) ?? new HashSet<Guid>();

            views.Add(new AnnouncementView(
                announcement.Id,
                announcement.Title,
                announcement.Body,
                AnnouncementCategories.NameOf(announcement.Category),
                AnnouncementCategories.NameOf(announcement.Scope),
                announcement.Block,
                announcement.UnitId,
                announcement.CreatedAt,
                announcement.UpdatedAt,
                audience.Count,
                audience.Count(read.Contains),
                records[i].Version));
        }

        return views;
    }

    public static AnnouncementViewer ViewerOf(Guid userId, IReadOnlyList<ResidentUnit> units) =>
        new(userId, [.. units.Select(unit => unit.Block).OfType<string>().Distinct()], [.. units.Select(unit => unit.UnitId)]);
}
