using Microsoft.Extensions.Logging.Abstractions;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Application.Announcements;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using Urban.TestSupport.Time;

namespace Urban.UnitTests.Operations.Application;

/// <summary>
/// Tudo o que um handler de comunicado precisa, já ligado: o condomínio da requisição, o relógio (22/11/2026 às 12h UTC), o condomínio
/// de mentira (blocos, unidades e quem mora onde) e os comunicados em memória. Cada teste monta só o que quer provar.
/// </summary>
internal sealed class AnnouncementTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    public Guid Manager { get; } = Guid.CreateVersion7();

    public ITenantContext Tenant { get; private set; }

    public FakeClock Clock { get; } = new(Now);

    public FakeAnnouncements Announcements { get; } = new();

    public FakeAnnouncementAudience Directory { get; } = new();

    public FakeAnnouncementMailDispatcher Mail { get; } = new();

    public FakeAnnouncementEvents Events { get; } = new();

    /// <summary>O notificador de verdade, ligado ao condomínio e aos avisos de mentira.</summary>
    public AnnouncementLiveNotifier Live => new(Directory, Events, Clock, NullLogger<AnnouncementLiveNotifier>.Instance);

    public AnnouncementTestKit()
    {
        Tenant = new FixedTenant(CondominiumId);
    }

    public AnnouncementTestKit WithoutATenant()
    {
        Tenant = new FixedTenant(null);

        return this;
    }

    /// <summary>Uma unidade do condomínio de mentira.</summary>
    public ResidentUnit NewUnit(string? block, string number) => Directory.AddUnit(block, number);

    /// <summary>Uma conta que mora hoje na unidade.</summary>
    public Guid NewResident(ResidentUnit unit)
    {
        Guid account = Guid.CreateVersion7();
        Directory.Live(account, unit);

        return account;
    }

    /// <summary>Um comunicado já guardado, escrito pelo síndico, com o alcance dado.</summary>
    public Announcement NewAnnouncement(AnnouncementAudience? audience = null, string title = "Aviso", AnnouncementCategory category = AnnouncementCategory.General, Guid? author = null, DateTimeOffset? at = null)
    {
        Announcement announcement = Announcement.Create(CondominiumId, title, "Texto do aviso.", category, audience ?? AnnouncementAudience.WholeCondominium, author ?? Manager, at ?? Now).Value;
        Announcements.Seed(announcement);

        return announcement;
    }

    /// <summary>Um comunicado já guardado em que o síndico pediu o e-mail ao publicar.</summary>
    public Announcement NewAnnouncementWithEmail(AnnouncementAudience? audience = null, string title = "Aviso", AnnouncementCategory category = AnnouncementCategory.General, string body = "Texto do aviso.")
    {
        Announcement announcement = Announcement.Create(CondominiumId, title, body, category, audience ?? AnnouncementAudience.WholeCondominium, Manager, Now, notifyByEmail: true).Value;
        Announcements.Seed(announcement);

        return announcement;
    }

    public static AnnouncementAudience Block(string name) => AnnouncementAudience.OfBlock(name).Value;

    public static AnnouncementAudience Unit(ResidentUnit unit) => AnnouncementAudience.OfUnit(unit.UnitId).Value;
}

/// <summary>Os comunicados e as leituras em memória, com a mesma regra de visibilidade, de página e de versão do PostgreSQL.</summary>
internal sealed class FakeAnnouncements : IAnnouncements
{
    private readonly List<Announcement> _items = [];
    private readonly Dictionary<Guid, int> _versions = [];
    private readonly List<(Guid AnnouncementId, Guid UserId, DateTimeOffset At)> _reads = [];

    public IReadOnlyList<Announcement> All => _items;

    public IReadOnlyList<(Guid AnnouncementId, Guid UserId, DateTimeOffset At)> Reads => _reads;

    public int ListReadersCalls { get; private set; }

    public int ListVisibleCalls { get; private set; }

    public int MarkAllReadCalls { get; private set; }

    public int SaveCalls { get; private set; }

    /// <summary>Faz o próximo <see cref="SaveAsync"/>, <see cref="DeleteAsync"/> ou <see cref="MarkReadAsync"/> falhar com o erro dado.</summary>
    public Error? FailNextWrite { get; set; }

    public Announcement Seed(Announcement announcement)
    {
        _items.Add(announcement);
        _versions[announcement.Id] = 1;

        return announcement;
    }

    public void SeedRead(Announcement announcement, Guid userId, DateTimeOffset at) => _reads.Add((announcement.Id, userId, at));

    public string VersionOf(Announcement announcement) => _versions[announcement.Id].ToString(System.Globalization.CultureInfo.InvariantCulture);

    public Task<AnnouncementRecord?> FindAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        Announcement? found = _items.SingleOrDefault(item => item.Id == announcementId);

        return Task.FromResult(found is null ? null : new AnnouncementRecord(found, VersionOf(found)));
    }

    public Task<PagedAnnouncements> ListAsync(int page, int pageSize, CancellationToken cancellationToken) => Task.FromResult(Page(_items, page, pageSize));

    public Task<PagedAnnouncements> ListVisibleAsync(AnnouncementViewer viewer, MuralFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        ListVisibleCalls++;
        IEnumerable<Announcement> visible = Visible(viewer);

        if (filter.UnreadOnly)
        {
            visible = visible.Where(item => !HasRead(item, viewer.UserId));
        }

        if (filter.Category is { } category)
        {
            visible = visible.Where(item => item.Category == category);
        }

        return Task.FromResult(Page(visible, page, pageSize));
    }

    public Task<UnreadCounts> CountUnreadAsync(AnnouncementViewer viewer, CancellationToken cancellationToken)
    {
        List<Announcement> unread = [.. Visible(viewer).Where(item => !HasRead(item, viewer.UserId))];

        return Task.FromResult(new UnreadCounts(
            unread.Count(item => item.Category == AnnouncementCategory.General),
            unread.Count(item => item.Category == AnnouncementCategory.Maintenance),
            unread.Count(item => item.Category == AnnouncementCategory.Important)));
    }

    public Task<int> MarkAllReadAsync(AnnouncementViewer viewer, AnnouncementCategory? category, DateTimeOffset now, CancellationToken cancellationToken)
    {
        MarkAllReadCalls++;
        List<Announcement> pending = [.. Visible(viewer).Where(item => !HasRead(item, viewer.UserId) && (category is null || item.Category == category))];

        foreach (Announcement item in pending)
        {
            _reads.Add((item.Id, viewer.UserId, now));
        }

        return Task.FromResult(pending.Count);
    }

    public Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> ListReadersAsync(IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken)
    {
        ListReadersCalls++;

        return Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>>(_reads
            .Where(read => announcementIds.Contains(read.AnnouncementId))
            .GroupBy(read => read.AnnouncementId)
            .ToDictionary(group => group.Key, group => (IReadOnlySet<Guid>)group.Select(read => read.UserId).ToHashSet()));
    }

    public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListReadAtAsync(Guid userId, IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, DateTimeOffset>>(_reads
            .Where(read => read.UserId == userId && announcementIds.Contains(read.AnnouncementId))
            .ToDictionary(read => read.AnnouncementId, read => read.At));

    public Task<Result<string>> AddAsync(Announcement announcement, CancellationToken cancellationToken)
    {
        Seed(announcement);

        return Task.FromResult(Result.Success(VersionOf(announcement)));
    }

    public Task<Result<string>> SaveAsync(AnnouncementRecord record, CancellationToken cancellationToken)
    {
        SaveCalls++;

        if (TakeFailure() is { } error)
        {
            return Task.FromResult(Result.Failure<string>(error));
        }

        _versions[record.Announcement.Id]++;

        return Task.FromResult(Result.Success(VersionOf(record.Announcement)));
    }

    public Task<Result> DeleteAsync(AnnouncementRecord record, CancellationToken cancellationToken)
    {
        if (TakeFailure() is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        _items.Remove(record.Announcement);
        _reads.RemoveAll(read => read.AnnouncementId == record.Announcement.Id);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> MarkReadAsync(Guid announcementId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (TakeFailure() is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        if (_items.All(item => item.Id != announcementId))
        {
            return Task.FromResult(Result.Failure(AnnouncementErrors.NotFound));
        }

        if (!_reads.Any(read => read.AnnouncementId == announcementId && read.UserId == userId))
        {
            _reads.Add((announcementId, userId, now));
        }

        return Task.FromResult(Result.Success());
    }

    private Error? TakeFailure()
    {
        Error? error = FailNextWrite;
        FailNextWrite = null;

        return error;
    }

    private IEnumerable<Announcement> Visible(AnnouncementViewer viewer) =>
        _items.Where(item => item.CreatedBy != viewer.UserId && item.Audience.Reaches(viewer.Blocks, viewer.UnitIds));

    private bool HasRead(Announcement item, Guid userId) => _reads.Any(read => read.AnnouncementId == item.Id && read.UserId == userId);

    private PagedAnnouncements Page(IEnumerable<Announcement> source, int page, int pageSize)
    {
        List<Announcement> ordered = [.. source.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)];

        return new PagedAnnouncements(
            [.. ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(item => new AnnouncementRecord(item, VersionOf(item)))],
            ordered.Count);
    }
}

/// <summary>O condomínio de mentira dos comunicados: blocos, unidades e quem mora hoje em cada uma, com as chamadas contadas.</summary>
internal sealed class FakeAnnouncementAudience : IAnnouncementAudience
{
    private readonly List<ResidentUnit> _units = [];
    private readonly List<(Guid UserId, Guid UnitId)> _living = [];

    public int RecipientsCalls { get; private set; }

    public DateOnly? LastToday { get; private set; }

    public int Queries { get; private set; }

    /// <summary>Faz a próxima consulta de moradores por alcance falhar, como um banco fora do ar.</summary>
    public Exception? FailRecipients { get; set; }

    public ResidentUnit AddUnit(string? block, string number)
    {
        ResidentUnit unit = new(Guid.CreateVersion7(), block, number);
        _units.Add(unit);

        return unit;
    }

    public void Live(Guid userId, ResidentUnit unit) => _living.Add((userId, unit.UnitId));

    public Task<bool> BlockExistsAsync(string block, CancellationToken cancellationToken)
    {
        Queries++;

        return Task.FromResult(_units.Any(unit => unit.Block == block));
    }

    public Task<ResidentUnit?> FindUnitAsync(Guid unitId, CancellationToken cancellationToken)
    {
        Queries++;

        return Task.FromResult(_units.SingleOrDefault(unit => unit.UnitId == unitId));
    }

    public Task<IReadOnlyList<ResidentUnit>> GetUnitsAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        Queries++;
        LastToday = today;

        return Task.FromResult<IReadOnlyList<ResidentUnit>>([.. _units.Where(unit => _living.Any(entry => entry.UserId == userId && entry.UnitId == unit.UnitId))]);
    }

    public Task<IReadOnlyList<IReadOnlySet<Guid>>> GetRecipientsAsync(IReadOnlyList<AudienceTarget> targets, DateOnly today, CancellationToken cancellationToken)
    {
        Queries++;
        RecipientsCalls++;
        LastToday = today;

        if (FailRecipients is not null)
        {
            throw FailRecipients;
        }

        return Task.FromResult<IReadOnlyList<IReadOnlySet<Guid>>>([.. targets.Select(target => (IReadOnlySet<Guid>)ResidentsOf(target).ToHashSet())]);
    }

    private IEnumerable<Guid> ResidentsOf(AudienceTarget target) =>
        _living
            .Where(entry => _units.Single(unit => unit.UnitId == entry.UnitId) is var unit
                && (target.UnitId is { } wanted ? unit.UnitId == wanted : target.Block is not { } block || unit.Block == block))
            .Select(entry => entry.UserId);
}

/// <summary>Guarda os pedidos de e-mail em vez de enfileirá-los, para o teste ver o que seria pedido ao segundo plano.</summary>
internal sealed class FakeAnnouncementMailDispatcher : IAnnouncementMailDispatcher
{
    public List<(Guid CondominiumId, Guid AnnouncementId)> FanOuts { get; } = [];

    public List<(Guid CondominiumId, Guid AnnouncementId, Guid UserId)> Deliveries { get; } = [];

    public Task EnqueueFanOutAsync(Guid condominiumId, Guid announcementId, CancellationToken cancellationToken)
    {
        FanOuts.Add((condominiumId, announcementId));

        return Task.CompletedTask;
    }

    public Task EnqueueDeliveryAsync(Guid condominiumId, Guid announcementId, Guid userId, CancellationToken cancellationToken)
    {
        Deliveries.Add((condominiumId, announcementId, userId));

        return Task.CompletedTask;
    }
}

/// <summary>Guarda os avisos em tempo real em vez de mandá-los, para o teste ver quem seria avisado.</summary>
internal sealed class FakeAnnouncementEvents : IAnnouncementEvents
{
    public List<(AnnouncementChange Change, IReadOnlySet<Guid> Users)> Sent { get; } = [];

    /// <summary>Faz o próximo envio falhar, como um servidor de tempo real fora do ar.</summary>
    public Exception? FailNext { get; set; }

    public Task SendAsync(AnnouncementChange change, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (FailNext is not null)
        {
            Exception failure = FailNext;
            FailNext = null;

            throw failure;
        }

        Sent.Add((change, userIds.ToHashSet()));

        return Task.CompletedTask;
    }
}
