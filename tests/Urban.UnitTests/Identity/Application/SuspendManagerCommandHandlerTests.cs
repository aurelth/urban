using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Administration;
using Urban.Identity.Application.Administration.SuspendManager;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class SuspendManagerCommandHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Later = UserBuilder.Now.AddHours(2);

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IManagerAccounts> _managers = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ISessionRevocations> _sessions = new();
    private readonly Mock<IClock> _clock = new();
    private readonly SuspendManagerCommandHandler _handler;
    private readonly User _admin;

    public SuspendManagerCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Later);
        _admin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com");
        _accounts.Setup(a => a.FindByIdAsync(_admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_admin);
        _managers
            .Setup(m => m.TryChangeStatusAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new SuspendManagerCommandHandler(_accounts.Object, _managers.Object, _refreshTokens.Object, _sessions.Object, _clock.Object);
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Deactivated)]
    public async Task Handle_SuspendsTheManagerSavesItConditionedOnTheStatusItWasReadIn(AccountStatus current)
    {
        User manager = Target(current);

        Result result = await _handler.Handle(new SuspendManagerCommand(_admin.Id, manager.Id, "  Uso indevido da plataforma  "), Ct);

        result.IsSuccess.Should().BeTrue();
        manager.Status.Should().Be(AccountStatus.Suspended);
        manager.StatusReason.Should().Be("Uso indevido da plataforma");
        manager.StatusChangedBy.Should().Be(_admin.Id);
        manager.StatusChangedAt.Should().Be(Later);
        _managers.Verify(m => m.TryChangeStatusAsync(manager, current, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EndsEverySessionOfTheManagerRightAfterSavingTheSuspension()
    {
        User manager = Target(AccountStatus.Active);
        List<string> order = [];
        _managers
            .Setup(m => m.TryChangeStatusAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("saved"))
            .ReturnsAsync(true);
        _refreshTokens
            .Setup(r => r.RevokeAllOfUserAsync(manager.Id, Later, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("refresh"))
            .Returns(Task.CompletedTask);
        _sessions
            .Setup(s => s.RevokeAllOfUserAsync(manager.Id, Later, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("access"))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SuspendManagerCommand(_admin.Id, manager.Id, "Motivo"), Ct);

        order.Should().Equal("saved", "refresh", "access");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithoutAReason_FailsAndChangesNothing(string? reason)
    {
        User manager = Target(AccountStatus.Active);

        Result result = await _handler.Handle(new SuspendManagerCommand(_admin.Id, manager.Id, reason), Ct);

        result.Error.Should().Be(UserErrors.ReasonRequired);
        NothingWasWrittenOrRevoked(manager, AccountStatus.Active);
    }

    [Fact]
    public async Task Handle_WithAReasonLongerThanTheLimit_FailsAndChangesNothing()
    {
        User manager = Target(AccountStatus.Active);

        Result result = await _handler.Handle(new SuspendManagerCommand(_admin.Id, manager.Id, new string('a', User.MaxReasonLength + 1)), Ct);

        result.Error.Should().Be(UserErrors.ReasonTooLong);
        NothingWasWrittenOrRevoked(manager, AccountStatus.Active);
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.PendingApproval)]
    [InlineData(AccountStatus.Rejected)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_ForAManagerWhoCannotBeSuspendedFromThatStatus_ReturnsInvalidTransitionAndChangesNothing(AccountStatus current)
    {
        User manager = Target(current);

        Result result = await _handler.Handle(new SuspendManagerCommand(_admin.Id, manager.Id, "Motivo"), Ct);

        result.Error.Should().Be(UserErrors.InvalidTransition);
        NothingWasWrittenOrRevoked(manager, current);
    }

    [Fact]
    public async Task Handle_WhenAnotherDecisionWinsTheRace_ReturnsInvalidTransitionAndKeepsTheSessions()
    {
        User manager = Target(AccountStatus.Active);
        _managers
            .Setup(m => m.TryChangeStatusAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(new SuspendManagerCommand(_admin.Id, manager.Id, "Motivo"), Ct);

        result.Error.Should().Be(UserErrors.InvalidTransition);
        _refreshTokens.VerifyNoOtherCalls();
        _sessions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Handle_ForAnAccountThatIsNotAManager_ReturnsNotFoundAndChangesNothing(UserRole role)
    {
        User other = Target(AccountStatus.Active, role);

        Result result = await _handler.Handle(new SuspendManagerCommand(_admin.Id, other.Id, "Motivo"), Ct);

        result.Error.Should().Be(AdministrationErrors.ManagerNotFound);
        NothingWasWrittenOrRevoked(other, AccountStatus.Active);
    }

    [Fact]
    public async Task Handle_ForAnUnknownAccount_ReturnsNotFound()
    {
        Result result = await _handler.Handle(new SuspendManagerCommand(_admin.Id, Guid.CreateVersion7(), "Motivo"), Ct);

        result.Error.Should().Be(AdministrationErrors.ManagerNotFound);
        _managers.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Deactivated)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveSuperAdmin_IsForbiddenAndChangesNothing(UserRole role, AccountStatus status)
    {
        User actor = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        User manager = Target(AccountStatus.Active);

        Result result = await _handler.Handle(new SuspendManagerCommand(actor.Id, manager.Id, "Motivo"), Ct);

        result.Error.Should().Be(AdministrationErrors.Forbidden);
        NothingWasWrittenOrRevoked(manager, AccountStatus.Active);
    }

    private User Target(AccountStatus status, UserRole role = UserRole.Manager)
    {
        User user = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    private void NothingWasWrittenOrRevoked(User user, AccountStatus expectedStatus)
    {
        user.Status.Should().Be(expectedStatus);
        _managers.VerifyNoOtherCalls();
        _refreshTokens.VerifyNoOtherCalls();
        _sessions.VerifyNoOtherCalls();
    }
}
