using FluentAssertions;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Application.Announcements;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Operations.Application;

public class AnnouncementWriteHandlersTests
{
    private readonly AnnouncementTestKit _kit = new();

    private CreateAnnouncementCommandHandler CreateHandler() => new(_kit.Announcements, _kit.Directory, _kit.Mail, _kit.Tenant, _kit.Clock, _kit.Live);

    private UpdateAnnouncementCommandHandler UpdateHandler() => new(_kit.Announcements, _kit.Directory, _kit.Tenant, _kit.Clock, _kit.Live);

    private DeleteAnnouncementCommandHandler DeleteHandler() => new(_kit.Announcements, _kit.Tenant, _kit.Live);

    private CreateAnnouncementCommand Create(
        string? title = "Corte de água",
        string? body = "Sábado, das 8h às 12h.",
        string? category = "maintenance",
        string? scope = "condominium",
        string? block = null,
        Guid? unitId = null,
        bool notifyByEmail = false) =>
        new(_kit.Manager, title, body, category, scope, block, unitId, notifyByEmail);

    private static UpdateAnnouncementCommand Update(
        Announcement announcement,
        string? version,
        string? title = "Novo título",
        string? body = "Novo texto.",
        string? category = "important",
        string? scope = "condominium",
        string? block = null,
        Guid? unitId = null) =>
        new(announcement.Id, version, title, body, category, scope, block, unitId);

    // ---- Criar ----

    [Fact]
    public async Task Create_ForTheWholeCondominium_StoresItAndCountsWhoLivesThere()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        _kit.NewResident(a101);
        _kit.NewResident(b201);

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Corte de água");
        result.Value.Body.Should().Be("Sábado, das 8h às 12h.");
        result.Value.Category.Should().Be("maintenance");
        result.Value.Scope.Should().Be("condominium");
        result.Value.Block.Should().BeNull();
        result.Value.UnitId.Should().BeNull();
        result.Value.CreatedAt.Should().Be(AnnouncementTestKit.Now);
        result.Value.UpdatedAt.Should().BeNull();
        result.Value.Recipients.Should().Be(2);
        result.Value.Readers.Should().Be(0);
        result.Value.Version.Should().Be("1");
        _kit.Announcements.All.Should().ContainSingle().Which.CreatedBy.Should().Be(_kit.Manager);
        _kit.Announcements.All[0].CondominiumId.Should().Be(_kit.CondominiumId);
    }

    [Fact]
    public async Task Create_WithoutAskingForEmail_StoresItWithoutEmailAndEnqueuesNothing()
    {
        Result<AnnouncementView> result = await CreateHandler().Handle(Create(), default);

        result.Value.NotifiedByEmail.Should().BeFalse();
        _kit.Announcements.All.Single().NotifyByEmail.Should().BeFalse();
        _kit.Mail.FanOuts.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AskingForEmail_RemembersItAndEnqueuesOneFanOutForTheCondominium()
    {
        Result<AnnouncementView> result = await CreateHandler().Handle(Create(category: "important", notifyByEmail: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.NotifiedByEmail.Should().BeTrue();
        _kit.Announcements.All.Single().NotifyByEmail.Should().BeTrue();
        _kit.Mail.FanOuts.Should().ContainSingle().Which.Should().Be((_kit.CondominiumId, result.Value.Id));
        _kit.Mail.Deliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AskingForEmailWithAnInvalidText_EnqueuesNothing()
    {
        Result<AnnouncementView> result = await CreateHandler().Handle(Create(title: "", notifyByEmail: true), default);

        result.IsFailure.Should().BeTrue();
        _kit.Mail.FanOuts.Should().BeEmpty();
        _kit.Announcements.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AskingForEmailForABlockThatDoesNotExist_EnqueuesNothing()
    {
        _kit.NewUnit("Bloco A", "101");

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(scope: "block", block: "Bloco Z", notifyByEmail: true), default);

        result.Error.Should().Be(AnnouncementErrors.BlockNotFound);
        _kit.Mail.FanOuts.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_NeverAsksForEmailAgain_AndKeepsWhatWasAsked()
    {
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result<AnnouncementView> result = await UpdateHandler().Handle(Update(announcement, _kit.Announcements.VersionOf(announcement)), default);

        result.Value.NotifiedByEmail.Should().BeTrue();
        _kit.Mail.FanOuts.Should().BeEmpty();
        _kit.Mail.Deliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ForABlock_CountsOnlyWhoLivesInThatBlock()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit a102 = _kit.NewUnit("Bloco A", "102");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        _kit.NewResident(a101);
        _kit.NewResident(a102);
        _kit.NewResident(b201);

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(scope: "block", block: "Bloco A"), default);

        result.Value.Scope.Should().Be("block");
        result.Value.Block.Should().Be("Bloco A");
        result.Value.Recipients.Should().Be(2);
    }

    [Fact]
    public async Task Create_ForAUnit_CountsOnlyWhoLivesInThatUnit_AndEveryoneInIt()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit a102 = _kit.NewUnit("Bloco A", "102");
        _kit.NewResident(a101);
        _kit.NewResident(a101);
        _kit.NewResident(a102);

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(scope: "unit", unitId: a101.UnitId), default);

        result.Value.Scope.Should().Be("unit");
        result.Value.UnitId.Should().Be(a101.UnitId);
        result.Value.Recipients.Should().Be(2);
    }

    [Fact]
    public async Task Create_TheAuthorWhoLivesInTheScope_IsNotCountedAsARecipient()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.Directory.Live(_kit.Manager, a101);
        _kit.NewResident(a101);

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(), default);

        result.Value.Recipients.Should().Be(1);
    }

    [Fact]
    public async Task Create_AccountsLivingInSeveralUnitsOfTheScope_AreCountedOnce()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit a102 = _kit.NewUnit("Bloco A", "102");
        Guid owner = _kit.NewResident(a101);
        _kit.Directory.Live(owner, a102);

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(), default);

        result.Value.Recipients.Should().Be(1);
    }

    [Fact]
    public async Task Create_AsksWhoLivesThereTwice_OnceForTheCountsAndOnceForTheLiveNotice_AndWithTodayInBrasilia()
    {
        _kit.Clock.Advance(TimeSpan.FromHours(13));

        await CreateHandler().Handle(Create(), default);

        _kit.Directory.RecipientsCalls.Should().Be(2);
        _kit.Directory.LastToday.Should().Be(new DateOnly(2026, 11, 22));
    }

    [Fact]
    public async Task Create_TodayIsTheDayInBrasilia_NotInUtc()
    {
        _kit.Clock.Advance(TimeSpan.FromHours(13).Add(TimeSpan.FromMinutes(30)));

        await CreateHandler().Handle(Create(), default);

        _kit.Directory.LastToday.Should().Be(new DateOnly(2026, 11, 22));

        _kit.Clock.Advance(TimeSpan.FromHours(2));

        await CreateHandler().Handle(Create(), default);

        _kit.Directory.LastToday.Should().Be(new DateOnly(2026, 11, 23));
    }

    [Fact]
    public async Task Create_ForABlockThatHasNoUnits_Fails_AndStoresNothing()
    {
        _kit.NewUnit("Bloco A", "101");

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(scope: "block", block: "Bloco Z"), default);

        result.Error.Should().Be(AnnouncementErrors.BlockNotFound);
        _kit.Announcements.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ForAUnitThatDoesNotExist_Fails_AndStoresNothing()
    {
        Result<AnnouncementView> result = await CreateHandler().Handle(Create(scope: "unit", unitId: Guid.CreateVersion7()), default);

        result.Error.Should().Be(AnnouncementErrors.UnitNotFound);
        _kit.Announcements.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_TheBlockIsComparedAsTheSyndicTypedItAfterNormalizing()
    {
        _kit.NewUnit("Bloco A", "101");

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(scope: "block", block: "  Bloco   A "), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Block.Should().Be("Bloco A");
    }

    [Theory]
    [InlineData("", "Texto", "general", "condominium", "announcement.title.invalid")]
    [InlineData("Título", "", "general", "condominium", "announcement.body.invalid")]
    [InlineData("Título", "Texto", "urgent", "condominium", "announcement.category.invalid")]
    [InlineData("Título", "Texto", "general", "everyone", "announcement.scope.invalid")]
    [InlineData("Título", "Texto", "general", "block", "announcement.block.invalid")]
    [InlineData("Título", "Texto", "general", "unit", "announcement.unit.required")]
    public async Task Create_InvalidData_FailsBeforeAskingTheCondominiumAnything(string title, string body, string category, string scope, string code)
    {
        Result<AnnouncementView> result = await CreateHandler().Handle(Create(title, body, category, scope), default);

        result.Error.Code.Should().Be(code);
        _kit.Announcements.All.Should().BeEmpty();
        _kit.Directory.Queries.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithoutACondominium_Fails()
    {
        Result<AnnouncementView> result = await new CreateAnnouncementCommandHandler(_kit.Announcements, _kit.Directory, _kit.Mail, _kit.WithoutATenant().Tenant, _kit.Clock, _kit.Live).Handle(Create(), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
        _kit.Announcements.All.Should().BeEmpty();
    }

    // ---- Editar ----

    [Fact]
    public async Task Update_ReplacesTheAnnouncement_StampsTheMoment_AndReturnsTheNewVersion()
    {
        Announcement announcement = _kit.NewAnnouncement(title: "Antigo");
        _kit.Clock.Advance(TimeSpan.FromHours(2));

        Result<AnnouncementView> result = await UpdateHandler().Handle(Update(announcement, "1"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Novo título");
        result.Value.Body.Should().Be("Novo texto.");
        result.Value.Category.Should().Be("important");
        result.Value.UpdatedAt.Should().Be(AnnouncementTestKit.Now.AddHours(2));
        result.Value.CreatedAt.Should().Be(AnnouncementTestKit.Now);
        result.Value.Version.Should().Be("2");
        announcement.Title.Should().Be("Novo título");
        _kit.Announcements.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Update_KeepsWhoAlreadyRead()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid reader = _kit.NewResident(a101);
        _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement();
        _kit.Announcements.SeedRead(announcement, reader, AnnouncementTestKit.Now);

        Result<AnnouncementView> result = await UpdateHandler().Handle(Update(announcement, "1"), default);

        result.Value.Readers.Should().Be(1);
        result.Value.Recipients.Should().Be(2);
        _kit.Announcements.Reads.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_CanNarrowTheAudienceToABlock_AndTheCountFollowsIt()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        _kit.NewResident(a101);
        _kit.NewResident(b201);
        Announcement announcement = _kit.NewAnnouncement();

        Result<AnnouncementView> result = await UpdateHandler().Handle(Update(announcement, "1", scope: "block", block: "Bloco B"), default);

        result.Value.Scope.Should().Be("block");
        result.Value.Block.Should().Be("Bloco B");
        result.Value.Recipients.Should().Be(1);
    }

    [Fact]
    public async Task Update_WithoutTheVersion_Fails()
    {
        Announcement announcement = _kit.NewAnnouncement();

        (await UpdateHandler().Handle(Update(announcement, null), default)).Error.Should().Be(OperationsErrors.VersionRequired);
        (await UpdateHandler().Handle(Update(announcement, "  "), default)).Error.Should().Be(OperationsErrors.VersionRequired);
        _kit.Announcements.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Update_AnAnnouncementThatDoesNotExist_Fails()
    {
        Result<AnnouncementView> result = await UpdateHandler().Handle(new UpdateAnnouncementCommand(Guid.CreateVersion7(), "1", "T", "B", "general", "condominium", null, null), default);

        result.Error.Should().Be(AnnouncementErrors.NotFound);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_IsAConflict_AndSavesNothing()
    {
        Announcement announcement = _kit.NewAnnouncement(title: "Antigo");

        Result<AnnouncementView> result = await UpdateHandler().Handle(Update(announcement, "7"), default);

        result.Error.Should().Be(OperationsErrors.ConcurrencyConflict);
        announcement.Title.Should().Be("Antigo");
        _kit.Announcements.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenSomeoneSavesInTheMeantime_IsAConflict()
    {
        Announcement announcement = _kit.NewAnnouncement();
        _kit.Announcements.FailNextWrite = OperationsErrors.ConcurrencyConflict;

        Result<AnnouncementView> result = await UpdateHandler().Handle(Update(announcement, "1"), default);

        result.Error.Should().Be(OperationsErrors.ConcurrencyConflict);
    }

    [Theory]
    [InlineData("", "Texto", "general", "condominium", "announcement.title.invalid")]
    [InlineData("Título", "   ", "general", "condominium", "announcement.body.invalid")]
    [InlineData("Título", "Texto", "urgent", "condominium", "announcement.category.invalid")]
    [InlineData("Título", "Texto", "general", "everyone", "announcement.scope.invalid")]
    public async Task Update_InvalidData_FailsAndSavesNothing(string title, string body, string category, string scope, string code)
    {
        Announcement announcement = _kit.NewAnnouncement();

        Result<AnnouncementView> result = await UpdateHandler().Handle(Update(announcement, "1", title, body, category, scope), default);

        result.Error.Code.Should().Be(code);
        _kit.Announcements.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Update_ToABlockOrUnitThatDoesNotExist_FailsAndSavesNothing()
    {
        Announcement announcement = _kit.NewAnnouncement();

        (await UpdateHandler().Handle(Update(announcement, "1", scope: "block", block: "Bloco Z"), default)).Error.Should().Be(AnnouncementErrors.BlockNotFound);
        (await UpdateHandler().Handle(Update(announcement, "1", scope: "unit", unitId: Guid.CreateVersion7()), default)).Error.Should().Be(AnnouncementErrors.UnitNotFound);
        _kit.Announcements.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Update_WithoutACondominium_Fails()
    {
        Announcement announcement = _kit.NewAnnouncement();

        Result<AnnouncementView> result = await new UpdateAnnouncementCommandHandler(_kit.Announcements, _kit.Directory, _kit.WithoutATenant().Tenant, _kit.Clock, _kit.Live).Handle(Update(announcement, "1"), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
    }

    // ---- Apagar ----

    [Fact]
    public async Task Delete_RemovesTheAnnouncementAndItsReads_AndLeavesTheOthers()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid reader = _kit.NewResident(a101);
        Announcement doomed = _kit.NewAnnouncement(title: "Apagar");
        Announcement kept = _kit.NewAnnouncement(title: "Ficar");
        _kit.Announcements.SeedRead(doomed, reader, AnnouncementTestKit.Now);
        _kit.Announcements.SeedRead(kept, reader, AnnouncementTestKit.Now);

        Result result = await DeleteHandler().Handle(new DeleteAnnouncementCommand(doomed.Id, "1"), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Announcements.All.Should().Equal(kept);
        _kit.Announcements.Reads.Should().ContainSingle().Which.AnnouncementId.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Delete_WithoutTheVersion_Fails_AndRemovesNothing()
    {
        Announcement announcement = _kit.NewAnnouncement();

        (await DeleteHandler().Handle(new DeleteAnnouncementCommand(announcement.Id, null), default)).Error.Should().Be(OperationsErrors.VersionRequired);
        _kit.Announcements.All.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_AnAnnouncementThatDoesNotExist_Fails()
    {
        (await DeleteHandler().Handle(new DeleteAnnouncementCommand(Guid.CreateVersion7(), "1"), default)).Error.Should().Be(AnnouncementErrors.NotFound);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_IsAConflict_AndRemovesNothing()
    {
        Announcement announcement = _kit.NewAnnouncement();

        Result result = await DeleteHandler().Handle(new DeleteAnnouncementCommand(announcement.Id, "9"), default);

        result.Error.Should().Be(OperationsErrors.ConcurrencyConflict);
        _kit.Announcements.All.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_WithoutACondominium_Fails()
    {
        Announcement announcement = _kit.NewAnnouncement();

        Result result = await new DeleteAnnouncementCommandHandler(_kit.Announcements, _kit.WithoutATenant().Tenant, _kit.Live).Handle(new DeleteAnnouncementCommand(announcement.Id, "1"), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
        _kit.Announcements.All.Should().ContainSingle();
    }

    // ---- Aviso em tempo real ----

    [Fact]
    public async Task Create_TellsTheResidentsOfTheAudienceInRealTime_ButNotTheManagerWhoWroteIt()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid ana = _kit.NewResident(a101);
        _kit.NewResident(b201);
        _kit.Directory.Live(_kit.Manager, a101);

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(scope: "block", block: "Bloco A"), default);

        (AnnouncementChange change, IReadOnlySet<Guid> users) = _kit.Events.Sent.Should().ContainSingle().Subject;
        change.Should().Be(new AnnouncementChange(AnnouncementChangeKind.Published, result.Value.Id));
        users.Should().BeEquivalentTo([ana]);
    }

    [Fact]
    public async Task Create_ThatFails_TellsNobody()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.NewResident(a101);

        await CreateHandler().Handle(Create(title: ""), default);
        await CreateHandler().Handle(Create(scope: "block", block: "Bloco que não existe"), default);

        _kit.Events.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_StillSucceeds_WhenTheRealTimeNoticeFails()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.NewResident(a101);
        _kit.Events.FailNext = new InvalidOperationException("hub fora do ar");

        Result<AnnouncementView> result = await CreateHandler().Handle(Create(), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Announcements.All.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_TellsTheResidentsOfBothTheOldAndTheNewAudience()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        ResidentUnit c301 = _kit.NewUnit("Bloco C", "301");
        Guid ana = _kit.NewResident(a101);
        Guid caio = _kit.NewResident(b201);
        _kit.NewResident(c301);
        Announcement announcement = _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco A"));

        await UpdateHandler().Handle(Update(announcement, "1", scope: "block", block: "Bloco B"), default);

        (AnnouncementChange change, IReadOnlySet<Guid> users) = _kit.Events.Sent.Should().ContainSingle().Subject;
        change.Should().Be(new AnnouncementChange(AnnouncementChangeKind.Updated, announcement.Id));
        users.Should().BeEquivalentTo([ana, caio]);
    }

    [Fact]
    public async Task Update_ThatFails_TellsNobody()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement();

        await UpdateHandler().Handle(Update(announcement, "9"), default);
        await UpdateHandler().Handle(Update(announcement, "1", title: ""), default);
        _kit.Announcements.FailNextWrite = OperationsErrors.ConcurrencyConflict;
        await UpdateHandler().Handle(Update(announcement, "1"), default);

        _kit.Events.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_TellsTheResidentsOfTheAudienceThatItWasRemoved()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid ana = _kit.NewResident(a101);
        _kit.NewResident(b201);
        Announcement announcement = _kit.NewAnnouncement(AnnouncementTestKit.Block("Bloco A"));

        Result result = await DeleteHandler().Handle(new DeleteAnnouncementCommand(announcement.Id, "1"), default);

        result.IsSuccess.Should().BeTrue();
        (AnnouncementChange change, IReadOnlySet<Guid> users) = _kit.Events.Sent.Should().ContainSingle().Subject;
        change.Should().Be(new AnnouncementChange(AnnouncementChangeKind.Removed, announcement.Id));
        users.Should().BeEquivalentTo([ana]);
    }

    [Fact]
    public async Task Delete_ThatFails_TellsNobody()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement();

        await DeleteHandler().Handle(new DeleteAnnouncementCommand(announcement.Id, "9"), default);
        await DeleteHandler().Handle(new DeleteAnnouncementCommand(Guid.CreateVersion7(), "1"), default);
        _kit.Announcements.FailNextWrite = OperationsErrors.ConcurrencyConflict;
        await DeleteHandler().Handle(new DeleteAnnouncementCommand(announcement.Id, "1"), default);

        _kit.Events.Sent.Should().BeEmpty();
    }
}
