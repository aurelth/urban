using FluentAssertions;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.Operations.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Operations.Persistence;

[Collection(OperationsPostgresCollection.Name)]
public class OperationsAnnouncementsTests(OperationsPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly OperationsStoreKit _kit = new(postgres);

    private static DateTimeOffset Day(int offset) => OperationsStoreKit.Now.AddDays(offset);

    private static AnnouncementViewer Viewer(Guid user, string[]? blocks = null, Guid[]? units = null) => new(user, blocks ?? [], units ?? []);

    // ---- Guardar e ler ----

    [Fact]
    public async Task Add_ThenFind_ReturnsTheAnnouncementWithItsVersion()
    {
        Announcement announcement = Announcement.Create(
            _kit.CondominiumId, "Corte de água", "Sábado, das 8h às 12h.", AnnouncementCategory.Maintenance, OperationsStoreKit.Block("Bloco A"), _kit.Manager, OperationsStoreKit.Now).Value;

        Result<string> added = await _kit.Announcements().AddAsync(announcement, Ct);
        AnnouncementRecord? found = await _kit.Announcements().FindAsync(announcement.Id, Ct);

        added.IsSuccess.Should().BeTrue();
        found.Should().NotBeNull();
        found!.Version.Should().Be(added.Value);
        found.Announcement.Title.Should().Be("Corte de água");
        found.Announcement.Body.Should().Be("Sábado, das 8h às 12h.");
        found.Announcement.Category.Should().Be(AnnouncementCategory.Maintenance);
        found.Announcement.Scope.Should().Be(AnnouncementScope.Block);
        found.Announcement.Block.Should().Be("Bloco A");
        found.Announcement.UnitId.Should().BeNull();
        found.Announcement.CondominiumId.Should().Be(_kit.CondominiumId);
        found.Announcement.CreatedBy.Should().Be(_kit.Manager);
        found.Announcement.CreatedAt.Should().Be(OperationsStoreKit.Now);
        found.Announcement.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Add_AnAnnouncementForAUnit_KeepsTheUnitAndNoBlock()
    {
        Guid unit = Guid.CreateVersion7();
        Announcement stored = await _kit.StoreAsync(OperationsStoreKit.Unit(unit));

        AnnouncementRecord found = (await _kit.Announcements().FindAsync(stored.Id, Ct))!;

        found.Announcement.Scope.Should().Be(AnnouncementScope.Unit);
        found.Announcement.UnitId.Should().Be(unit);
        found.Announcement.Block.Should().BeNull();
    }

    [Fact]
    public async Task Find_FromAnotherCondominium_OrWithoutATenant_ReturnsNothing()
    {
        Announcement stored = await _kit.StoreAsync();

        (await _kit.Announcements(Guid.CreateVersion7()).FindAsync(stored.Id, Ct)).Should().BeNull();
        (await _kit.WithoutATenant().FindAsync(stored.Id, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task Find_AnUnknownId_ReturnsNothing()
    {
        (await _kit.Announcements().FindAsync(Guid.CreateVersion7(), Ct)).Should().BeNull();
    }

    // ---- Editar e a versão ----

    [Fact]
    public async Task Save_AnEditedAnnouncement_PersistsItAndChangesTheVersion()
    {
        Announcement stored = await _kit.StoreAsync(title: "Antigo");
        OperationsAnnouncements repository = _kit.Announcements();
        AnnouncementRecord record = (await repository.FindAsync(stored.Id, Ct))!;
        DateTimeOffset later = OperationsStoreKit.Now.AddHours(4);
        record.Announcement.Edit("Novo", "Texto novo.", AnnouncementCategory.Important, OperationsStoreKit.Block("Bloco B"), later).IsSuccess.Should().BeTrue();

        Result<string> saved = await repository.SaveAsync(record, Ct);
        AnnouncementRecord reread = (await _kit.Announcements().FindAsync(stored.Id, Ct))!;

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(record.Version).And.Be(reread.Version);
        reread.Announcement.Title.Should().Be("Novo");
        reread.Announcement.Category.Should().Be(AnnouncementCategory.Important);
        reread.Announcement.Scope.Should().Be(AnnouncementScope.Block);
        reread.Announcement.Block.Should().Be("Bloco B");
        reread.Announcement.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public async Task Save_FromABlockToTheWholeCondominium_ClearsTheBlockInTheDatabase()
    {
        Announcement stored = await _kit.StoreAsync(OperationsStoreKit.Block("Bloco A"));
        OperationsAnnouncements repository = _kit.Announcements();
        AnnouncementRecord record = (await repository.FindAsync(stored.Id, Ct))!;
        record.Announcement.Edit("Aviso", "Texto.", AnnouncementCategory.General, AnnouncementAudience.WholeCondominium, OperationsStoreKit.Now).IsSuccess.Should().BeTrue();

        (await repository.SaveAsync(record, Ct)).IsSuccess.Should().BeTrue();

        AnnouncementRecord reread = (await _kit.Announcements().FindAsync(stored.Id, Ct))!;
        reread.Announcement.Scope.Should().Be(AnnouncementScope.Condominium);
        reread.Announcement.Block.Should().BeNull();
    }

    [Fact]
    public async Task Save_OverAnEditMadeMeanwhile_IsAConflict_AndKeepsTheOtherEdit()
    {
        Announcement stored = await _kit.StoreAsync(title: "Original");
        OperationsAnnouncements first = _kit.Announcements();
        OperationsAnnouncements second = _kit.Announcements();
        AnnouncementRecord mine = (await first.FindAsync(stored.Id, Ct))!;
        AnnouncementRecord theirs = (await second.FindAsync(stored.Id, Ct))!;
        theirs.Announcement.Edit("Da outra pessoa", "Texto.", AnnouncementCategory.General, AnnouncementAudience.WholeCondominium, OperationsStoreKit.Now).IsSuccess.Should().BeTrue();
        (await second.SaveAsync(theirs, Ct)).IsSuccess.Should().BeTrue();
        mine.Announcement.Edit("Minha", "Texto.", AnnouncementCategory.General, AnnouncementAudience.WholeCondominium, OperationsStoreKit.Now).IsSuccess.Should().BeTrue();

        Result<string> result = await first.SaveAsync(mine, Ct);

        result.Error.Should().Be(OperationsErrors.ConcurrencyConflict);
        (await _kit.Announcements().FindAsync(stored.Id, Ct))!.Announcement.Title.Should().Be("Da outra pessoa");
    }

    // ---- Apagar ----

    [Fact]
    public async Task Delete_RemovesTheAnnouncementAndItsReads_AndLeavesTheOthers()
    {
        Announcement doomed = await _kit.StoreAsync(title: "Apagar");
        Announcement kept = await _kit.StoreAsync(title: "Ficar");
        Guid reader = Guid.CreateVersion7();
        (await _kit.Announcements().MarkReadAsync(doomed.Id, reader, OperationsStoreKit.Now, Ct)).IsSuccess.Should().BeTrue();
        (await _kit.Announcements().MarkReadAsync(kept.Id, reader, OperationsStoreKit.Now, Ct)).IsSuccess.Should().BeTrue();
        OperationsAnnouncements repository = _kit.Announcements();
        AnnouncementRecord record = (await repository.FindAsync(doomed.Id, Ct))!;

        Result result = await repository.DeleteAsync(record, Ct);

        result.IsSuccess.Should().BeTrue();
        (await _kit.Announcements().FindAsync(doomed.Id, Ct)).Should().BeNull();
        (await _kit.Announcements().FindAsync(kept.Id, Ct)).Should().NotBeNull();
        (await _kit.Announcements().ListReadersAsync([doomed.Id, kept.Id], Ct)).Keys.Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Delete_AnAnnouncementEditedMeanwhile_IsAConflict_AndKeepsIt()
    {
        Announcement stored = await _kit.StoreAsync();
        OperationsAnnouncements first = _kit.Announcements();
        OperationsAnnouncements second = _kit.Announcements();
        AnnouncementRecord mine = (await first.FindAsync(stored.Id, Ct))!;
        AnnouncementRecord theirs = (await second.FindAsync(stored.Id, Ct))!;
        theirs.Announcement.Edit("Editado", "Texto.", AnnouncementCategory.General, AnnouncementAudience.WholeCondominium, OperationsStoreKit.Now).IsSuccess.Should().BeTrue();
        (await second.SaveAsync(theirs, Ct)).IsSuccess.Should().BeTrue();

        Result result = await first.DeleteAsync(mine, Ct);

        result.Error.Should().Be(OperationsErrors.ConcurrencyConflict);
        (await _kit.Announcements().FindAsync(stored.Id, Ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_AnAnnouncementAlreadyDeleted_IsNotFound()
    {
        Announcement stored = await _kit.StoreAsync();
        OperationsAnnouncements first = _kit.Announcements();
        OperationsAnnouncements second = _kit.Announcements();
        AnnouncementRecord mine = (await first.FindAsync(stored.Id, Ct))!;
        AnnouncementRecord theirs = (await second.FindAsync(stored.Id, Ct))!;
        (await second.DeleteAsync(theirs, Ct)).IsSuccess.Should().BeTrue();

        Result result = await first.DeleteAsync(mine, Ct);

        result.Error.Should().Be(AnnouncementErrors.NotFound);
    }

    // ---- Lista do síndico ----

    [Fact]
    public async Task List_ReturnsTheNewestFirst_WithTheTotal_AndEachVersion()
    {
        Announcement old = await _kit.StoreAsync(title: "Velho", at: Day(-2));
        Announcement newest = await _kit.StoreAsync(title: "Novo", at: Day(0));
        Announcement middle = await _kit.StoreAsync(title: "Meio", at: Day(-1));

        PagedAnnouncements page = await _kit.Announcements().ListAsync(1, 20, Ct);

        page.Total.Should().Be(3);
        page.Items.Select(item => item.Announcement.Id).Should().Equal(newest.Id, middle.Id, old.Id);
        page.Items.Select(item => item.Version).Should().OnlyContain(version => version.Length > 0);
        (await _kit.Announcements().FindAsync(newest.Id, Ct))!.Version.Should().Be(page.Items[0].Version);
    }

    [Fact]
    public async Task List_ReturnsOnePageAtATime()
    {
        for (int i = 0; i < 5; i++)
        {
            await _kit.StoreAsync(title: $"Aviso {i}", at: Day(-i));
        }

        PagedAnnouncements second = await _kit.Announcements().ListAsync(2, 2, Ct);
        PagedAnnouncements last = await _kit.Announcements().ListAsync(3, 2, Ct);
        PagedAnnouncements beyond = await _kit.Announcements().ListAsync(4, 2, Ct);

        second.Items.Select(item => item.Announcement.Title).Should().Equal("Aviso 2", "Aviso 3");
        last.Items.Select(item => item.Announcement.Title).Should().Equal("Aviso 4");
        beyond.Items.Should().BeEmpty();
        second.Total.Should().Be(5);
        beyond.Total.Should().Be(5);
    }

    [Fact]
    public async Task List_ShowsOnlyTheCondominiumOfTheRequest()
    {
        await _kit.StoreAsync(title: "Nosso");
        Guid other = Guid.CreateVersion7();
        await _kit.StoreAsync(title: "Deles", condominiumId: other);

        PagedAnnouncements ours = await _kit.Announcements().ListAsync(1, 20, Ct);
        PagedAnnouncements none = await _kit.WithoutATenant().ListAsync(1, 20, Ct);

        ours.Items.Select(item => item.Announcement.Title).Should().Equal("Nosso");
        ours.Total.Should().Be(1);
        none.Items.Should().BeEmpty();
        none.Total.Should().Be(0);
    }

    // ---- Mural ----

    [Fact]
    public async Task ListVisible_ReturnsTheWholeCondominium_TheBlockAndTheUnit_AndNothingElse()
    {
        Guid myUnit = Guid.CreateVersion7();
        Announcement everyone = await _kit.StoreAsync(title: "Todos", at: Day(-4));
        Announcement myBlock = await _kit.StoreAsync(OperationsStoreKit.Block("Bloco A"), title: "Meu bloco", at: Day(-3));
        Announcement mine = await _kit.StoreAsync(OperationsStoreKit.Unit(myUnit), title: "Minha unidade", at: Day(-2));
        await _kit.StoreAsync(OperationsStoreKit.Block("Bloco B"), title: "Outro bloco", at: Day(-1));
        await _kit.StoreAsync(OperationsStoreKit.Unit(Guid.CreateVersion7()), title: "Outra unidade", at: Day(0));
        AnnouncementViewer viewer = Viewer(Guid.CreateVersion7(), ["Bloco A"], [myUnit]);

        PagedAnnouncements page = await _kit.Announcements().ListVisibleAsync(viewer, false, 1, 20, Ct);

        page.Items.Select(item => item.Announcement.Id).Should().Equal(mine.Id, myBlock.Id, everyone.Id);
        page.Total.Should().Be(3);
    }

    [Fact]
    public async Task ListVisible_ABlockNameIsComparedExactly()
    {
        await _kit.StoreAsync(OperationsStoreKit.Block("Bloco A"), title: "Bloco A");
        await _kit.StoreAsync(OperationsStoreKit.Block("Bloco AB"), title: "Bloco AB");

        PagedAnnouncements page = await _kit.Announcements().ListVisibleAsync(Viewer(Guid.CreateVersion7(), ["Bloco A"]), false, 1, 20, Ct);

        page.Items.Select(item => item.Announcement.Title).Should().Equal("Bloco A");
    }

    [Fact]
    public async Task ListVisible_LeavesOutWhatTheViewerWroteHimself()
    {
        Announcement theirs = await _kit.StoreAsync(title: "De outro síndico", author: Guid.CreateVersion7());
        await _kit.StoreAsync(title: "Meu");

        PagedAnnouncements page = await _kit.Announcements().ListVisibleAsync(Viewer(_kit.Manager), false, 1, 20, Ct);

        page.Items.Select(item => item.Announcement.Id).Should().Equal(theirs.Id);
    }

    [Fact]
    public async Task ListVisible_UnreadOnly_LeavesOutWhatTheViewerAlreadyRead_AndTheTotalFollows()
    {
        Guid me = Guid.CreateVersion7();
        Announcement read = await _kit.StoreAsync(title: "Lido", at: Day(-1));
        Announcement unread = await _kit.StoreAsync(title: "Não lido", at: Day(0));
        (await _kit.Announcements().MarkReadAsync(read.Id, me, OperationsStoreKit.Now, Ct)).IsSuccess.Should().BeTrue();
        (await _kit.Announcements().MarkReadAsync(unread.Id, Guid.CreateVersion7(), OperationsStoreKit.Now, Ct)).IsSuccess.Should().BeTrue();

        PagedAnnouncements page = await _kit.Announcements().ListVisibleAsync(Viewer(me), true, 1, 20, Ct);

        page.Items.Select(item => item.Announcement.Id).Should().Equal(unread.Id);
        page.Total.Should().Be(1);
    }

    [Fact]
    public async Task ListVisible_IsPaged()
    {
        for (int i = 0; i < 5; i++)
        {
            await _kit.StoreAsync(title: $"Aviso {i}", at: Day(-i));
        }

        PagedAnnouncements second = await _kit.Announcements().ListVisibleAsync(Viewer(Guid.CreateVersion7()), false, 2, 2, Ct);

        second.Items.Select(item => item.Announcement.Title).Should().Equal("Aviso 2", "Aviso 3");
        second.Total.Should().Be(5);
    }

    [Fact]
    public async Task ListVisible_NeverCrossesTheCondominium()
    {
        await _kit.StoreAsync(title: "Nosso");
        await _kit.StoreAsync(title: "Deles", condominiumId: Guid.CreateVersion7());

        PagedAnnouncements page = await _kit.Announcements().ListVisibleAsync(Viewer(Guid.CreateVersion7()), false, 1, 20, Ct);

        page.Items.Select(item => item.Announcement.Title).Should().Equal("Nosso");
    }

    [Fact]
    public async Task CountUnread_CountsTheWholeBoardOfTheViewer()
    {
        Guid me = Guid.CreateVersion7();
        Announcement first = await _kit.StoreAsync();
        await _kit.StoreAsync();
        await _kit.StoreAsync(OperationsStoreKit.Block("Bloco B"));
        await _kit.StoreAsync(author: me);
        (await _kit.Announcements().MarkReadAsync(first.Id, me, OperationsStoreKit.Now, Ct)).IsSuccess.Should().BeTrue();

        int unread = await _kit.Announcements().CountUnreadAsync(Viewer(me, ["Bloco A"]), Ct);

        unread.Should().Be(1);
    }

    // ---- Leituras ----

    [Fact]
    public async Task MarkRead_StoresTheReadWithTheMoment()
    {
        Announcement stored = await _kit.StoreAsync();
        Guid me = Guid.CreateVersion7();

        Result result = await _kit.Announcements().MarkReadAsync(stored.Id, me, OperationsStoreKit.Now.AddHours(1), Ct);

        result.IsSuccess.Should().BeTrue();
        IReadOnlyDictionary<Guid, DateTimeOffset> readAt = await _kit.Announcements().ListReadAtAsync(me, [stored.Id], Ct);
        readAt.Should().ContainKey(stored.Id).WhoseValue.Should().Be(OperationsStoreKit.Now.AddHours(1));
    }

    [Fact]
    public async Task MarkRead_Twice_IsSuccess_AndKeepsTheFirstMoment()
    {
        Announcement stored = await _kit.StoreAsync();
        Guid me = Guid.CreateVersion7();
        await _kit.Announcements().MarkReadAsync(stored.Id, me, OperationsStoreKit.Now, Ct);

        Result again = await _kit.Announcements().MarkReadAsync(stored.Id, me, OperationsStoreKit.Now.AddDays(1), Ct);

        again.IsSuccess.Should().BeTrue();
        (await _kit.Announcements().ListReadAtAsync(me, [stored.Id], Ct))[stored.Id].Should().Be(OperationsStoreKit.Now);
    }

    [Fact]
    public async Task MarkRead_ManyAtTheSameTime_LeavesASingleRead_AndEveryoneSucceeds()
    {
        Announcement stored = await _kit.StoreAsync();
        Guid me = Guid.CreateVersion7();

        Result[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _kit.Announcements().MarkReadAsync(stored.Id, me, OperationsStoreKit.Now, Ct)));

        results.Should().OnlyContain(result => result.IsSuccess);
        (await _kit.Announcements().ListReadersAsync([stored.Id], Ct))[stored.Id].Should().Equal(me);
    }

    [Fact]
    public async Task MarkRead_AnAnnouncementThatDoesNotExistOrBelongsToAnotherCondominium_IsNotFound()
    {
        Announcement stored = await _kit.StoreAsync();
        Guid me = Guid.CreateVersion7();

        (await _kit.Announcements().MarkReadAsync(Guid.CreateVersion7(), me, OperationsStoreKit.Now, Ct)).Error.Should().Be(AnnouncementErrors.NotFound);
        (await _kit.Announcements(Guid.CreateVersion7()).MarkReadAsync(stored.Id, me, OperationsStoreKit.Now, Ct)).Error.Should().Be(AnnouncementErrors.NotFound);
        (await _kit.WithoutATenant().MarkReadAsync(stored.Id, me, OperationsStoreKit.Now, Ct)).Error.Should().Be(AnnouncementErrors.NotFound);
        (await _kit.Announcements().ListReadersAsync([stored.Id], Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task ListReaders_GroupsTheAccountsByAnnouncement_AndLeavesOutWhatNobodyRead()
    {
        Announcement a = await _kit.StoreAsync();
        Announcement b = await _kit.StoreAsync();
        Announcement unread = await _kit.StoreAsync();
        Guid ana = Guid.CreateVersion7();
        Guid bia = Guid.CreateVersion7();
        await _kit.Announcements().MarkReadAsync(a.Id, ana, OperationsStoreKit.Now, Ct);
        await _kit.Announcements().MarkReadAsync(a.Id, bia, OperationsStoreKit.Now, Ct);
        await _kit.Announcements().MarkReadAsync(b.Id, ana, OperationsStoreKit.Now, Ct);

        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> readers = await _kit.Announcements().ListReadersAsync([a.Id, b.Id, unread.Id], Ct);

        readers.Keys.Should().BeEquivalentTo([a.Id, b.Id]);
        readers[a.Id].Should().BeEquivalentTo([ana, bia]);
        readers[b.Id].Should().BeEquivalentTo([ana]);
    }

    [Fact]
    public async Task ListReadAt_ReturnsOnlyTheReadsOfThatAccount()
    {
        Announcement a = await _kit.StoreAsync();
        Announcement b = await _kit.StoreAsync();
        Guid ana = Guid.CreateVersion7();
        Guid bia = Guid.CreateVersion7();
        await _kit.Announcements().MarkReadAsync(a.Id, ana, OperationsStoreKit.Now, Ct);
        await _kit.Announcements().MarkReadAsync(b.Id, bia, OperationsStoreKit.Now, Ct);

        IReadOnlyDictionary<Guid, DateTimeOffset> readAt = await _kit.Announcements().ListReadAtAsync(ana, [a.Id, b.Id], Ct);

        readAt.Keys.Should().Equal(a.Id);
    }

    [Fact]
    public async Task TheReadsOfAnotherCondominium_AreInvisible()
    {
        Guid other = Guid.CreateVersion7();
        Announcement theirs = await _kit.StoreAsync(condominiumId: other);
        Guid me = Guid.CreateVersion7();
        (await _kit.Announcements(other).MarkReadAsync(theirs.Id, me, OperationsStoreKit.Now, Ct)).IsSuccess.Should().BeTrue();

        (await _kit.Announcements().ListReadersAsync([theirs.Id], Ct)).Should().BeEmpty();
        (await _kit.Announcements().ListReadAtAsync(me, [theirs.Id], Ct)).Should().BeEmpty();
        (await _kit.WithoutATenant().ListReadersAsync([theirs.Id], Ct)).Should().BeEmpty();
    }
}
