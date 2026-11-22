using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;

namespace Urban.Operations.Infrastructure.Persistence;

/// <summary>Os comunicados e as leituras no PostgreSQL, sempre dentro do condomínio da requisição (filtro de tenant do contexto).</summary>
public sealed class OperationsAnnouncements(OperationsDbContext db) : IAnnouncements
{
    private const string VersionProperty = "xmin";

    public async Task<AnnouncementRecord?> FindAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        Announcement? announcement = await db.Announcements.SingleOrDefaultAsync(item => item.Id == announcementId, cancellationToken);

        return announcement is null ? null : new AnnouncementRecord(announcement, CurrentVersion(announcement));
    }

    public async Task<PagedAnnouncements> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        await PageAsync(db.Announcements, page, pageSize, cancellationToken);

    public async Task<PagedAnnouncements> ListVisibleAsync(AnnouncementViewer viewer, MuralFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Announcement> visible = Visible(viewer);

        if (filter.UnreadOnly)
        {
            visible = Unread(visible, viewer.UserId);
        }

        if (filter.Category is { } category)
        {
            visible = visible.Where(announcement => announcement.Category == category);
        }

        return await PageAsync(visible, page, pageSize, cancellationToken);
    }

    public async Task<UnreadCounts> CountUnreadAsync(AnnouncementViewer viewer, CancellationToken cancellationToken)
    {
        var perCategory = await Unread(Visible(viewer), viewer.UserId)
            .GroupBy(announcement => announcement.Category)
            .Select(group => new { Category = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        int Of(AnnouncementCategory category) => perCategory.Where(row => row.Category == category).Sum(row => row.Count);

        return new UnreadCounts(Of(AnnouncementCategory.General), Of(AnnouncementCategory.Maintenance), Of(AnnouncementCategory.Important));
    }

    public async Task<int> MarkAllReadAsync(AnnouncementViewer viewer, AnnouncementCategory? category, DateTimeOffset now, CancellationToken cancellationToken)
    {
        while (true)
        {
            IQueryable<Announcement> pending = Unread(Visible(viewer), viewer.UserId);

            if (category is { } wanted)
            {
                pending = pending.Where(announcement => announcement.Category == wanted);
            }

            var rows = await pending.Select(announcement => new { announcement.Id, announcement.CondominiumId }).ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return 0;
            }

            db.AnnouncementReads.AddRange(rows.Select(row => new AnnouncementRead(row.Id, viewer.UserId, row.CondominiumId, now)));

            try
            {
                await db.SaveChangesAsync(cancellationToken);

                return rows.Count;
            }
            catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex) || PostgresErrors.IsMissingParentViolation(ex))
            {
                db.ChangeTracker.Clear();

                // Outra marcação (ou uma exclusão) passou na frente no meio do caminho: a gravação toda é desfeita, e a lista do que falta
                // é lida de novo sem o que já foi lido ou sumiu. Cada volta acontece porque alguém avançou, então o laço termina.
            }
        }
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> ListReadersAsync(IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. announcementIds];

        var reads = await db.AnnouncementReads
            .AsNoTracking()
            .Where(read => ids.Contains(read.AnnouncementId))
            .Select(read => new { read.AnnouncementId, read.UserId })
            .ToListAsync(cancellationToken);

        return reads
            .GroupBy(read => read.AnnouncementId)
            .ToDictionary(group => group.Key, group => (IReadOnlySet<Guid>)group.Select(read => read.UserId).ToHashSet());
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListReadAtAsync(Guid userId, IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. announcementIds];

        return await db.AnnouncementReads
            .AsNoTracking()
            .Where(read => read.UserId == userId && ids.Contains(read.AnnouncementId))
            .ToDictionaryAsync(read => read.AnnouncementId, read => read.ReadAt, cancellationToken);
    }

    public async Task<Result<string>> AddAsync(Announcement announcement, CancellationToken cancellationToken)
    {
        db.Announcements.Add(announcement);

        return await SaveAsync(announcement, cancellationToken);
    }

    public async Task<Result<string>> SaveAsync(AnnouncementRecord record, CancellationToken cancellationToken) =>
        await SaveAsync(record.Announcement, cancellationToken);

    public async Task<Result> DeleteAsync(AnnouncementRecord record, CancellationToken cancellationToken)
    {
        Guid id = record.Announcement.Id;
        db.Announcements.Remove(record.Announcement);

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            // O EF não diz se a linha mudou ou sumiu: quem apagou antes é "não encontrado", quem editou antes é conflito de versão.
            return await db.Announcements.AnyAsync(item => item.Id == id, cancellationToken)
                ? Result.Failure(OperationsErrors.ConcurrencyConflict)
                : Result.Failure(AnnouncementErrors.NotFound);
        }
    }

    public async Task<Result> MarkReadAsync(Guid announcementId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guid? condominiumId = await db.Announcements
            .Where(item => item.Id == announcementId)
            .Select(item => (Guid?)item.CondominiumId)
            .SingleOrDefaultAsync(cancellationToken);

        if (condominiumId is null)
        {
            return Result.Failure(AnnouncementErrors.NotFound);
        }

        if (await db.AnnouncementReads.AnyAsync(read => read.AnnouncementId == announcementId && read.UserId == userId, cancellationToken))
        {
            return Result.Success();
        }

        db.AnnouncementReads.Add(new AnnouncementRead(announcementId, userId, condominiumId.Value, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();

            // Duas marcações ao mesmo tempo: a outra já gravou, e o que importa (a conta leu) está feito.
            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsMissingParentViolation(ex))
        {
            db.ChangeTracker.Clear();

            return Result.Failure(AnnouncementErrors.NotFound);
        }
    }

    private IQueryable<Announcement> Visible(AnnouncementViewer viewer)
    {
        Guid user = viewer.UserId;
        string[] blocks = [.. viewer.Blocks];
        Guid[] units = [.. viewer.UnitIds];

        return db.Announcements.Where(announcement => announcement.CreatedBy != user
            && (announcement.Scope == AnnouncementScope.Condominium
                || (announcement.Scope == AnnouncementScope.Block && announcement.Block != null && blocks.Contains(announcement.Block))
                || (announcement.Scope == AnnouncementScope.Unit && announcement.UnitId != null && units.Contains(announcement.UnitId.Value))));
    }

    private IQueryable<Announcement> Unread(IQueryable<Announcement> announcements, Guid userId) =>
        announcements.Where(announcement => !db.AnnouncementReads.Any(read => read.AnnouncementId == announcement.Id && read.UserId == userId));

    private static async Task<PagedAnnouncements> PageAsync(IQueryable<Announcement> announcements, int page, int pageSize, CancellationToken cancellationToken)
    {
        int total = await announcements.CountAsync(cancellationToken);

        var rows = await announcements
            .AsNoTracking()
            .OrderByDescending(announcement => announcement.CreatedAt)
            .ThenByDescending(announcement => announcement.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(announcement => new { Announcement = announcement, Version = EF.Property<uint>(announcement, VersionProperty) })
            .ToListAsync(cancellationToken);

        return new PagedAnnouncements(
            [.. rows.Select(row => new AnnouncementRecord(row.Announcement, row.Version.ToString(CultureInfo.InvariantCulture)))],
            total);
    }

    private async Task<Result<string>> SaveAsync(Announcement announcement, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(CurrentVersion(announcement));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            return Result.Failure<string>(OperationsErrors.ConcurrencyConflict);
        }
    }

    private string CurrentVersion(Announcement announcement) =>
        db.Entry(announcement).Property<uint>(VersionProperty).CurrentValue.ToString(CultureInfo.InvariantCulture);
}
