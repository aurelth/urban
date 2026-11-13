using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Administration;
using Urban.Identity.Application.Administration.ReactivateManager;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ReactivateManagerCommandHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Later = UserBuilder.Now.AddHours(2);

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IManagerAccounts> _managers = new();
    private readonly Mock<IClock> _clock = new();
    private readonly ReactivateManagerCommandHandler _handler;
    private readonly User _admin;

    public ReactivateManagerCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Later);
        _admin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com");
        _accounts.Setup(a => a.FindByIdAsync(_admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_admin);
        _managers
            .Setup(m => m.TryChangeStatusAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new ReactivateManagerCommandHandler(_accounts.Object, _managers.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_ReactivatesASuspendedManagerClearsTheReasonAndSavesItConditionedOnTheSuspension()
    {
        User manager = Target(AccountStatus.Suspended);

        Result result = await _handler.Handle(new ReactivateManagerCommand(_admin.Id, manager.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        manager.Status.Should().Be(AccountStatus.Active);
        manager.StatusReason.Should().BeNull();
        manager.StatusChangedBy.Should().Be(_admin.Id);
        manager.StatusChangedAt.Should().Be(Later);
        _managers.Verify(m => m.TryChangeStatusAsync(manager, AccountStatus.Suspended, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.PendingApproval)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_ForAManagerWhoIsNotSuspended_ReturnsInvalidTransitionAndChangesNothing(AccountStatus current)
    {
        User manager = Target(current);

        Result result = await _handler.Handle(new ReactivateManagerCommand(_admin.Id, manager.Id), Ct);

        result.Error.Should().Be(UserErrors.InvalidTransition);
        manager.Status.Should().Be(current);
        _managers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenAnotherDecisionWinsTheRace_ReturnsInvalidTransition()
    {
        User manager = Target(AccountStatus.Suspended);
        _managers
            .Setup(m => m.TryChangeStatusAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(new ReactivateManagerCommand(_admin.Id, manager.Id), Ct);

        result.Error.Should().Be(UserErrors.InvalidTransition);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Handle_ForAnAccountThatIsNotAManager_ReturnsNotFoundAndChangesNothing(UserRole role)
    {
        User other = Target(AccountStatus.Suspended, role);

        Result result = await _handler.Handle(new ReactivateManagerCommand(_admin.Id, other.Id), Ct);

        result.Error.Should().Be(AdministrationErrors.ManagerNotFound);
        other.Status.Should().Be(AccountStatus.Suspended);
        _managers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownAccount_ReturnsNotFound()
    {
        Result result = await _handler.Handle(new ReactivateManagerCommand(_admin.Id, Guid.CreateVersion7()), Ct);

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
        User manager = Target(AccountStatus.Suspended);

        Result result = await _handler.Handle(new ReactivateManagerCommand(actor.Id, manager.Id), Ct);

        result.Error.Should().Be(AdministrationErrors.Forbidden);
        manager.Status.Should().Be(AccountStatus.Suspended);
        _managers.VerifyNoOtherCalls();
    }

    private User Target(AccountStatus status, UserRole role = UserRole.Manager)
    {
        User user = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
