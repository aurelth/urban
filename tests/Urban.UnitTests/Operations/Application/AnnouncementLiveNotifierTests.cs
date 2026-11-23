using FluentAssertions;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Application.Announcements;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Operations.Application;

public class AnnouncementLiveNotifierTests
{
    private static readonly Guid AnnouncementId = Guid.CreateVersion7();

    private readonly AnnouncementTestKit _kit = new();

    private Task NotifyAsync(AnnouncementChangeKind kind, params AnnouncementAudience[] audiences) =>
        _kit.Live.NotifyAsync(kind, AnnouncementId, _kit.Manager, audiences, default);

    [Fact]
    public async Task WholeCondominium_TellsEveryoneWhoLivesThere()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid ana = _kit.NewResident(a101);
        Guid caio = _kit.NewResident(b201);

        await NotifyAsync(AnnouncementChangeKind.Published, AnnouncementAudience.WholeCondominium);

        (AnnouncementChange change, IReadOnlySet<Guid> users) = _kit.Events.Sent.Should().ContainSingle().Subject;
        change.Should().Be(new AnnouncementChange(AnnouncementChangeKind.Published, AnnouncementId));
        users.Should().BeEquivalentTo([ana, caio]);
    }

    [Fact]
    public async Task ABlock_TellsOnlyWhoLivesInThatBlock()
    {
        Guid ana = _kit.NewResident(_kit.NewUnit("Bloco A", "101"));
        _kit.NewResident(_kit.NewUnit("Bloco B", "201"));

        await NotifyAsync(AnnouncementChangeKind.Updated, AnnouncementTestKit.Block("Bloco A"));

        _kit.Events.Sent.Should().ContainSingle().Which.Users.Should().BeEquivalentTo([ana]);
    }

    [Fact]
    public async Task AUnit_TellsOnlyWhoLivesInThatUnit()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid ana = _kit.NewResident(a101);
        _kit.NewResident(_kit.NewUnit("Bloco A", "102"));

        await NotifyAsync(AnnouncementChangeKind.Removed, AnnouncementTestKit.Unit(a101));

        _kit.Events.Sent.Should().ContainSingle().Which.Users.Should().BeEquivalentTo([ana]);
    }

    [Fact]
    public async Task TheAuthorIsNeverTold_EvenWhenLivingInTheAudience()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid ana = _kit.NewResident(a101);
        _kit.Directory.Live(_kit.Manager, a101);

        await NotifyAsync(AnnouncementChangeKind.Published, AnnouncementAudience.WholeCondominium);

        _kit.Events.Sent.Should().ContainSingle().Which.Users.Should().BeEquivalentTo([ana]);
    }

    [Fact]
    public async Task WhenOnlyTheAuthorLivesThere_NothingIsSent()
    {
        _kit.Directory.Live(_kit.Manager, _kit.NewUnit("Bloco A", "101"));

        await NotifyAsync(AnnouncementChangeKind.Published, AnnouncementAudience.WholeCondominium);

        _kit.Events.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenNobodyLivesThere_NothingIsSent()
    {
        _kit.NewUnit("Bloco A", "101");

        await NotifyAsync(AnnouncementChangeKind.Published, AnnouncementAudience.WholeCondominium);

        _kit.Events.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SeveralAudiences_AreAskedInOneQuery_AndTheAccountsAreJoinedWithoutRepeats()
    {
        Guid ana = _kit.NewResident(_kit.NewUnit("Bloco A", "101"));
        Guid caio = _kit.NewResident(_kit.NewUnit("Bloco B", "201"));
        _kit.NewResident(_kit.NewUnit("Bloco C", "301"));

        await NotifyAsync(AnnouncementChangeKind.Updated, AnnouncementTestKit.Block("Bloco A"), AnnouncementTestKit.Block("Bloco B"), AnnouncementAudience.WholeCondominium);

        _kit.Directory.RecipientsCalls.Should().Be(1);
        _kit.Events.Sent.Should().ContainSingle().Which.Users.Should().HaveCount(3).And.Contain([ana, caio]);
    }

    [Fact]
    public async Task OneAccountInTwoAudiences_IsToldOnce()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Guid ana = _kit.NewResident(a101);

        await NotifyAsync(AnnouncementChangeKind.Updated, AnnouncementTestKit.Block("Bloco A"), AnnouncementTestKit.Unit(a101));

        _kit.Events.Sent.Should().ContainSingle().Which.Users.Should().BeEquivalentTo([ana]);
    }

    [Fact]
    public async Task WhoLivesThereIsTodayInBrasilia_NotInUtc()
    {
        _kit.NewResident(_kit.NewUnit("Bloco A", "101"));
        _kit.Clock.Advance(TimeSpan.FromHours(13).Add(TimeSpan.FromMinutes(30)));

        await NotifyAsync(AnnouncementChangeKind.Published, AnnouncementAudience.WholeCondominium);

        _kit.Directory.LastToday.Should().Be(new DateOnly(2026, 11, 22));
    }

    [Fact]
    public async Task APushThatFails_NeverBreaksTheCaller()
    {
        _kit.NewResident(_kit.NewUnit("Bloco A", "101"));
        _kit.Events.FailNext = new InvalidOperationException("hub fora do ar");

        Func<Task> notify = () => NotifyAsync(AnnouncementChangeKind.Published, AnnouncementAudience.WholeCondominium);

        await notify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AQueryThatFails_NeverBreaksTheCaller_AndSendsNothing()
    {
        _kit.NewResident(_kit.NewUnit("Bloco A", "101"));
        _kit.Directory.FailRecipients = new InvalidOperationException("banco fora do ar");

        Func<Task> notify = () => NotifyAsync(AnnouncementChangeKind.Published, AnnouncementAudience.WholeCondominium);

        await notify.Should().NotThrowAsync();
        _kit.Events.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ACancelledRequest_IsNotSwallowed()
    {
        _kit.NewResident(_kit.NewUnit("Bloco A", "101"));
        using CancellationTokenSource cancelled = new();
        _kit.Events.FailNext = new OperationCanceledException(cancelled.Token);

        Func<Task> notify = () => _kit.Live.NotifyAsync(AnnouncementChangeKind.Published, AnnouncementId, _kit.Manager, [AnnouncementAudience.WholeCondominium], cancelled.Token);

        await notify.Should().ThrowAsync<OperationCanceledException>();
    }
}
