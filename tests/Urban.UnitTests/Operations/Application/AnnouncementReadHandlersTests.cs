using FluentAssertions;
using Urban.Operations.Application.Announcements;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Operations.Application;

public class AnnouncementReadHandlersTests
{
    private readonly AnnouncementTestKit _kit = new();

    private ListAnnouncementsQueryHandler ListHandler() => new(_kit.Announcements, _kit.Directory, _kit.Tenant, _kit.Clock);

    private ListMyAnnouncementsQueryHandler MineHandler() => new(_kit.Announcements, _kit.Directory, _kit.Tenant, _kit.Clock);

    private MarkAnnouncementReadCommandHandler MarkHandler() => new(_kit.Announcements, _kit.Directory, _kit.Tenant, _kit.Clock);

    private static DateTimeOffset Day(int offset) => AnnouncementTestKit.Now.AddDays(offset);

    // ---- Lista do síndico ----

    [Fact]
    public async Task List_ReturnsTheNewestFirst_WithThePageAndTheTotal()
    {
        Announcement old = _kit.NewAnnouncement(title: "Velho", at: Day(-2));
        Announcement newest = _kit.NewAnnouncement(title: "Novo", at: Day(0));
        Announcement middle = _kit.NewAnnouncement(title: "Meio", at: Day(-1));

        Result<AnnouncementPageView> result = await ListHandler().Handle(new ListAnnouncementsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(item => item.Id).Should().Equal(newest.Id, middle.Id, old.Id);
        result.Value.Total.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task List_ReturnsOnePageAtATime()
    {
        for (int i = 0; i < 5; i++)
        {
            _kit.NewAnnouncement(title: $"Aviso {i}", at: Day(-i));
        }

        Result<AnnouncementPageView> second = await ListHandler().Handle(new ListAnnouncementsQuery(2, 2), default);

        second.Value.Items.Select(item => item.Title).Should().Equal("Aviso 2", "Aviso 3");
        second.Value.Total.Should().Be(5);
    }

    [Fact]
    public async Task List_ShowsWhoReadOutOfWhoLivesThere_CountingOnlyCurrentResidents_AndLeavingTheAuthorOut()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid readsIt = _kit.NewResident(a101);
        _kit.NewResident(a101);
        Guid otherBlock = _kit.NewResident(b201);
        _kit.Directory.Live(_kit.Manager, a101);
        Guid movedOut = Guid.CreateVersion7();
        Announcement announcement = _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco A"));
        _kit.Announcements.SeedRead(announcement, readsIt, AnnouncementTestKit.Now);
        _kit.Announcements.SeedRead(announcement, otherBlock, AnnouncementTestKit.Now);
        _kit.Announcements.SeedRead(announcement, movedOut, AnnouncementTestKit.Now);
        _kit.Announcements.SeedRead(announcement, _kit.Manager, AnnouncementTestKit.Now);

        Result<AnnouncementPageView> result = await ListHandler().Handle(new ListAnnouncementsQuery(1, 20), default);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Recipients.Should().Be(2);
        result.Value.Items[0].Readers.Should().Be(1);
        result.Value.Items[0].Version.Should().Be("1");
        result.Value.Items[0].Scope.Should().Be("block");
        result.Value.Items[0].Block.Should().Be("Bloco A");
    }

    [Fact]
    public async Task List_CountsEveryAnnouncementWithTwoQueriesInTotal_NeverOnePerAnnouncement()
    {
        _kit.NewUnit("Bloco A", "101");
        for (int i = 0; i < 6; i++)
        {
            _kit.NewAnnouncement(at: Day(-i));
        }

        await ListHandler().Handle(new ListAnnouncementsQuery(1, 20), default);

        _kit.Directory.RecipientsCalls.Should().Be(1);
        _kit.Announcements.ListReadersCalls.Should().Be(1);
    }

    [Fact]
    public async Task List_AnEmptyCondominium_ReturnsAnEmptyPage_WithoutAskingForRecipients()
    {
        Result<AnnouncementPageView> result = await ListHandler().Handle(new ListAnnouncementsQuery(1, 20), default);

        result.Value.Items.Should().BeEmpty();
        result.Value.Total.Should().Be(0);
        _kit.Directory.RecipientsCalls.Should().Be(0);
        _kit.Announcements.ListReadersCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_AnInvalidPage_Fails(int page, int pageSize)
    {
        (await ListHandler().Handle(new ListAnnouncementsQuery(page, pageSize), default)).Error.Should().Be(AnnouncementErrors.PageInvalid);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(50, 20)]
    public async Task List_TheLimitsOfThePage_AreValid(int page, int pageSize)
    {
        (await ListHandler().Handle(new ListAnnouncementsQuery(page, pageSize), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task List_WithoutACondominium_Fails()
    {
        Result<AnnouncementPageView> result = await new ListAnnouncementsQueryHandler(_kit.Announcements, _kit.Directory, _kit.WithoutATenant().Tenant, _kit.Clock).Handle(new ListAnnouncementsQuery(1, 20), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
    }

    // ---- Mural ----

    [Fact]
    public async Task Mine_ShowsTheWholeCondominium_TheBlockAndTheUnitWhereTheAccountLives_AndNothingElse()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit a102 = _kit.NewUnit("Bloco A", "102");
        _kit.NewUnit("Bloco B", "201");
        Guid me = _kit.NewResident(a101);
        Announcement everyone = _kit.NewAnnouncement(title: "Todos", at: Day(-4));
        Announcement myBlock = _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco A"), title: "Meu bloco", at: Day(-3));
        Announcement myUnit = _kit.NewAnnouncement(AnnouncementTestKit.Unit(a101), title: "Minha unidade", at: Day(-2));
        _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco B"), title: "Outro bloco", at: Day(-1));
        _kit.NewAnnouncement(AnnouncementTestKit.Unit(a102), title: "Outra unidade", at: Day(0));

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(me, false, 1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(item => item.Id).Should().Equal(myUnit.Id, myBlock.Id, everyone.Id);
        result.Value.Total.Should().Be(3);
        result.Value.Unread.Should().Be(3);
    }

    [Fact]
    public async Task Mine_ADependentOrAnyoneWhoLivesInTheUnit_SeesItsAnnouncements()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.NewResident(a101);
        Guid dependent = _kit.NewResident(a101);
        _kit.NewAnnouncement(AnnouncementTestKit.Unit(a101));

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(dependent, false, 1, 20), default);

        result.Value.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Mine_SomeoneWhoLivesInTwoUnits_SeesTheAnnouncementsOfBothBlocks_OnceEach()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid me = _kit.NewResident(a101);
        _kit.Directory.Live(me, b201);
        _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco A"));
        _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco B"));
        _kit.NewAnnouncement();

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(me, false, 1, 20), default);

        result.Value.Total.Should().Be(3);
    }

    [Fact]
    public async Task Mine_UnitsWithoutABlock_OnlyGetTheCondominiumAndUnitAnnouncements()
    {
        ResidentUnit house = _kit.NewUnit(null, "7");
        Guid me = _kit.NewResident(house);
        _kit.NewAnnouncement(title: "Todos");
        _kit.NewAnnouncement(AnnouncementTestKit.Unit(house), title: "Casa 7");
        _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco A"), title: "Bloco A");

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(me, false, 1, 20), default);

        result.Value.Items.Select(item => item.Title).Should().BeEquivalentTo("Todos", "Casa 7");
    }

    [Fact]
    public async Task Mine_TheManagerWhoLivesThere_DoesNotSeeHisOwnAnnouncements_ButSeesAnotherManagersOnes()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.Directory.Live(_kit.Manager, a101);
        _kit.NewAnnouncement(title: "Meu");
        Announcement former = _kit.NewAnnouncement(title: "Do síndico anterior", author: Guid.CreateVersion7());

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(_kit.Manager, false, 1, 20), default);

        result.Value.Items.Select(item => item.Id).Should().Equal(former.Id);
    }

    [Fact]
    public async Task Mine_SomeoneWhoLivesNowhere_SeesNothing_EvenOfTheWholeCondominium()
    {
        _kit.NewUnit("Bloco A", "101");
        _kit.NewAnnouncement();

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(Guid.CreateVersion7(), false, 1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Total.Should().Be(0);
        result.Value.Unread.Should().Be(0);
        _kit.Announcements.ListVisibleCalls.Should().Be(0);
    }

    [Fact]
    public async Task Mine_MarksWhatWasRead_WithTheMoment_AndCountsTheUnreadOfTheWholeBoard()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        Announcement read = _kit.NewAnnouncement(title: "Lido", at: Day(-1));
        Announcement unread = _kit.NewAnnouncement(title: "Não lido", at: Day(0));
        DateTimeOffset readAt = AnnouncementTestKit.Now.AddHours(5);
        _kit.Announcements.SeedRead(read, me, readAt);
        _kit.Announcements.SeedRead(unread, Guid.CreateVersion7(), readAt);

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(me, false, 1, 20), default);

        result.Value.Items[0].Id.Should().Be(unread.Id);
        result.Value.Items[0].Read.Should().BeFalse();
        result.Value.Items[0].ReadAt.Should().BeNull();
        result.Value.Items[1].Id.Should().Be(read.Id);
        result.Value.Items[1].Read.Should().BeTrue();
        result.Value.Items[1].ReadAt.Should().Be(readAt);
        result.Value.Unread.Should().Be(1);
    }

    [Fact]
    public async Task Mine_TheUnreadCounterCoversTheWholeBoard_NotJustThePage()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        for (int i = 0; i < 5; i++)
        {
            _kit.NewAnnouncement(title: $"Aviso {i}", at: Day(-i));
        }

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(me, false, 2, 2), default);

        result.Value.Items.Select(item => item.Title).Should().Equal("Aviso 2", "Aviso 3");
        result.Value.Total.Should().Be(5);
        result.Value.Unread.Should().Be(5);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Mine_UnreadOnly_ShowsJustTheOnesNotRead_AndTheTotalFollowsTheFilter()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        Announcement read = _kit.NewAnnouncement(title: "Lido", at: Day(-1));
        Announcement unread = _kit.NewAnnouncement(title: "Não lido", at: Day(0));
        _kit.Announcements.SeedRead(read, me, AnnouncementTestKit.Now);

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(me, true, 1, 20), default);

        result.Value.Items.Select(item => item.Id).Should().Equal(unread.Id);
        result.Value.Total.Should().Be(1);
        result.Value.Unread.Should().Be(1);
    }

    [Fact]
    public async Task Mine_TheViewDoesNotCarryTheAuthorNorWhoRead()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        _kit.NewAnnouncement();

        Result<MyAnnouncementPageView> result = await MineHandler().Handle(new ListMyAnnouncementsQuery(me, false, 1, 20), default);

        typeof(MyAnnouncementView).GetProperties().Select(property => property.Name).Should().NotContain(["CreatedBy", "Readers", "Recipients", "Version", "UnitId", "Block"]);
        result.Value.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Mine_AskedForTodayInBrasilia()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        _kit.Clock.Advance(TimeSpan.FromHours(15).Add(TimeSpan.FromMinutes(30)));

        await MineHandler().Handle(new ListMyAnnouncementsQuery(me, false, 1, 20), default);

        _kit.Directory.LastToday.Should().Be(new DateOnly(2026, 11, 23));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Mine_AnInvalidPage_Fails_EvenForSomeoneWhoLivesNowhere(int page, int pageSize)
    {
        (await MineHandler().Handle(new ListMyAnnouncementsQuery(Guid.CreateVersion7(), false, page, pageSize), default)).Error.Should().Be(AnnouncementErrors.PageInvalid);
    }

    [Fact]
    public async Task Mine_WithoutACondominium_Fails()
    {
        Result<MyAnnouncementPageView> result = await new ListMyAnnouncementsQueryHandler(_kit.Announcements, _kit.Directory, _kit.WithoutATenant().Tenant, _kit.Clock)
            .Handle(new ListMyAnnouncementsQuery(Guid.CreateVersion7(), false, 1, 20), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
    }

    // ---- Marcar como lido ----

    [Fact]
    public async Task MarkRead_RecordsTheReadWithTheMoment()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement();

        Result result = await MarkHandler().Handle(new MarkAnnouncementReadCommand(me, announcement.Id), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Announcements.Reads.Should().ContainSingle().Which.Should().Be((announcement.Id, me, AnnouncementTestKit.Now));
    }

    [Fact]
    public async Task MarkRead_Twice_IsSuccess_AndKeepsTheFirstMoment()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement();
        await MarkHandler().Handle(new MarkAnnouncementReadCommand(me, announcement.Id), default);
        _kit.Clock.Advance(TimeSpan.FromHours(1));

        Result again = await MarkHandler().Handle(new MarkAnnouncementReadCommand(me, announcement.Id), default);

        again.IsSuccess.Should().BeTrue();
        _kit.Announcements.Reads.Should().ContainSingle().Which.At.Should().Be(AnnouncementTestKit.Now);
    }

    [Fact]
    public async Task MarkRead_ABlockAnnouncementOfTheBlockWhereTheAccountLives_Works()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco A"));

        (await MarkHandler().Handle(new MarkAnnouncementReadCommand(me, announcement.Id), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MarkRead_AnAnnouncementOfAnotherBlockOrUnit_LooksLikeItDoesNotExist()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid me = _kit.NewResident(a101);
        Announcement otherBlock = _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco B"));
        Announcement otherUnit = _kit.NewAnnouncement(AnnouncementTestKit.Unit(b201));

        (await MarkHandler().Handle(new MarkAnnouncementReadCommand(me, otherBlock.Id), default)).Error.Should().Be(AnnouncementErrors.NotFound);
        (await MarkHandler().Handle(new MarkAnnouncementReadCommand(me, otherUnit.Id), default)).Error.Should().Be(AnnouncementErrors.NotFound);
        _kit.Announcements.Reads.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_TheManagersOwnAnnouncement_LooksLikeItDoesNotExist()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.Directory.Live(_kit.Manager, a101);
        Announcement announcement = _kit.NewAnnouncement();

        (await MarkHandler().Handle(new MarkAnnouncementReadCommand(_kit.Manager, announcement.Id), default)).Error.Should().Be(AnnouncementErrors.NotFound);
        _kit.Announcements.Reads.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_SomeoneWhoLivesNowhere_CannotMarkAnything()
    {
        _kit.NewUnit("Bloco A", "101");
        Announcement announcement = _kit.NewAnnouncement();

        (await MarkHandler().Handle(new MarkAnnouncementReadCommand(Guid.CreateVersion7(), announcement.Id), default)).Error.Should().Be(AnnouncementErrors.NotFound);
        _kit.Announcements.Reads.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_AnAnnouncementThatDoesNotExist_Fails()
    {
        (await MarkHandler().Handle(new MarkAnnouncementReadCommand(Guid.CreateVersion7(), Guid.CreateVersion7()), default)).Error.Should().Be(AnnouncementErrors.NotFound);
    }

    [Fact]
    public async Task MarkRead_AnAnnouncementThatVanishesInTheMeantime_IsNotFound()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid me = _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement();
        _kit.Announcements.FailNextWrite = AnnouncementErrors.NotFound;

        (await MarkHandler().Handle(new MarkAnnouncementReadCommand(me, announcement.Id), default)).Error.Should().Be(AnnouncementErrors.NotFound);
    }

    [Fact]
    public async Task MarkRead_WithoutACondominium_Fails()
    {
        Result result = await new MarkAnnouncementReadCommandHandler(_kit.Announcements, _kit.Directory, _kit.WithoutATenant().Tenant, _kit.Clock)
            .Handle(new MarkAnnouncementReadCommand(Guid.CreateVersion7(), Guid.CreateVersion7()), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
    }
}
