using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Administration;
using Urban.Identity.Application.Administration.ListManagers;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ListManagersQueryHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IManagerAccounts> _managers = new();
    private readonly Mock<ICondominiumDirectory> _condominiums = new();
    private readonly ListManagersQueryHandler _handler;
    private readonly User _admin;

    public ListManagersQueryHandlerTests()
    {
        _admin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com");
        _accounts.Setup(a => a.FindByIdAsync(_admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_admin);
        _managers
            .Setup(m => m.ListAsync(It.IsAny<ManagerFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagerPage([], 0));

        _handler = new ListManagersQueryHandler(_accounts.Object, _managers.Object, _condominiums.Object);
    }

    [Fact]
    public async Task Handle_ShowsTheManagerWithTheRealNameOfTheCondominium()
    {
        User manager = UserBuilder.EnrolledManager("Ana", "ana@example.com");
        _managers
            .Setup(m => m.ListAsync(It.IsAny<ManagerFilter>(), 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagerPage([manager], 41));
        _condominiums
            .Setup(c => c.GetNamesAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { manager.CondominiumId!.Value })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [manager.CondominiumId!.Value] = "Residencial Vila Verde" });

        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(_admin.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        result.Value.Total.Should().Be(41);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        ManagerSummary item = result.Value.Items.Should().ContainSingle().Subject;
        item.Should().Be(new ManagerSummary(
            manager.Id,
            "ana@example.com",
            "Ana",
            manager.CondominiumId,
            "Residencial Vila Verde",
            AccountStatus.Active,
            null,
            manager.StatusChangedAt,
            manager.CreatedAt));
    }

    [Fact]
    public async Task Handle_ForAManagerWhoHasNoCondominiumYet_ShowsTheNameTypedInTheRequestAndDoesNotAskTheDirectory()
    {
        User approved = UserBuilder.Requested(address: "bia@example.com", name: "Bia", condominiumName: "Edifício Sol");
        approved.Approve(UserBuilder.ActorId, UserBuilder.Now);
        _managers
            .Setup(m => m.ListAsync(It.IsAny<ManagerFilter>(), 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagerPage([approved], 1));

        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(_admin.Id), Ct);

        ManagerSummary item = result.Value.Items.Single();
        item.CondominiumId.Should().BeNull();
        item.CondominiumName.Should().Be("Edifício Sol");
        item.Status.Should().Be(AccountStatus.PendingVerification);
        _condominiums.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheDirectoryDoesNotKnowTheCondominium_FallsBackToTheNameTypedInTheRequest()
    {
        User manager = UserBuilder.Requested(address: "bia@example.com", name: "Bia", condominiumName: "Edifício Sol");
        manager.Approve(UserBuilder.ActorId, UserBuilder.Now);
        manager.ConfirmEmail(UserBuilder.Now);
        manager.AssignCondominium(Guid.CreateVersion7(), DisplayName.Create("Bia").Value);
        _managers
            .Setup(m => m.ListAsync(It.IsAny<ManagerFilter>(), 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagerPage([manager], 1));
        _condominiums
            .Setup(c => c.GetNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(_admin.Id), Ct);

        result.Value.Items.Single().CondominiumName.Should().Be("Edifício Sol");
    }

    [Fact]
    public async Task Handle_ShowsWhySuspendedManagersWereSuspended()
    {
        User suspended = UserBuilder.InStatus(AccountStatus.Suspended, UserRole.Manager, "caio@example.com");
        _managers
            .Setup(m => m.ListAsync(It.IsAny<ManagerFilter>(), 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagerPage([suspended], 1));
        _condominiums
            .Setup(c => c.GetNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(_admin.Id), Ct);

        ManagerSummary item = result.Value.Items.Single();
        item.Status.Should().Be(AccountStatus.Suspended);
        item.StatusReason.Should().Be("Motivo de teste");
        item.Name.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PassesTheFiltersAndThePageToTheStore_TrimmingTheSearch()
    {
        await _handler.Handle(new ListManagersQuery(_admin.Id, AccountStatus.Suspended, "  ana  ", 2, 10), Ct);

        _managers.Verify(m => m.ListAsync(new ManagerFilter(AccountStatus.Suspended, "ana"), 2, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_TreatsABlankSearchAsNoSearch(string? search)
    {
        await _handler.Handle(new ListManagersQuery(_admin.Id, null, search), Ct);

        _managers.Verify(m => m.ListAsync(new ManagerFilter(null, null), 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_WithAnInvalidPage_FailsAndReadsNothing(int page, int pageSize)
    {
        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(_admin.Id, null, null, page, pageSize), Ct);

        result.Error.Should().Be(AdministrationErrors.PageInvalid);
        _managers.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountStatus.PendingApproval)]
    [InlineData(AccountStatus.Rejected)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_WithAStatusThatIsNotOfAManagerAccount_FailsAndReadsNothing(AccountStatus status)
    {
        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(_admin.Id, status), Ct);

        result.Error.Should().Be(AdministrationErrors.StatusFilterInvalid);
        _managers.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Deactivated)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveSuperAdmin_IsForbiddenAndReadsNothing(UserRole role, AccountStatus status)
    {
        User actor = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(actor.Id), Ct);

        result.Error.Should().Be(AdministrationErrors.Forbidden);
        _managers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownActor_IsForbidden()
    {
        Result<ManagerListing> result = await _handler.Handle(new ListManagersQuery(Guid.CreateVersion7()), Ct);

        result.Error.Should().Be(AdministrationErrors.Forbidden);
        _managers.VerifyNoOtherCalls();
    }
}
