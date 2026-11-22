using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;

namespace Urban.Operations.Infrastructure.Persistence;

/// <summary>Os comunicados e as leituras no PostgreSQL, sempre dentro do condomínio da requisição (filtro de tenant do contexto).</summary>
public sealed class OperationsAnnouncements(OperationsDbContext db) : IAnnouncements
{
    public Task<AnnouncementRecord?> FindAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        _ = db;
        throw new NotImplementedException();
    }

    public Task<PagedAnnouncements> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<PagedAnnouncements> ListVisibleAsync(AnnouncementViewer viewer, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<int> CountUnreadAsync(AnnouncementViewer viewer, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> ListReadersAsync(IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListReadAtAsync(Guid userId, IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Result<string>> AddAsync(Announcement announcement, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Result<string>> SaveAsync(AnnouncementRecord record, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Result> DeleteAsync(AnnouncementRecord record, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Result> MarkReadAsync(Guid announcementId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
