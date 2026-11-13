using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Application.RegistrationRequests.ApproveRegistration;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ApproveRegistrationCommandHandlerTests
{
    private static readonly DateTimeOffset Later = UserBuilder.Now.AddHours(2);

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IRegistrationRequests> _requests = new();
    private readonly Mock<IEmailVerificationDispatcher> _dispatcher = new();
    private readonly Mock<IClock> _clock = new();
    private readonly ApproveRegistrationCommandHandler _handler;
    private readonly User _admin;

    public ApproveRegistrationCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Later);
        _admin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com");
        _accounts.Setup(a => a.FindByIdAsync(_admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_admin);
        _requests
            .Setup(r => r.TryDecideAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new ApproveRegistrationCommandHandler(_accounts.Object, _requests.Object, _dispatcher.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_ForAPendingRequest_MovesItToPendingVerificationSavesItAndQueuesTheLink()
    {
        User request = Target(UserBuilder.Requested());

        Result result = await _handler.Handle(new ApproveRegistrationCommand(_admin.Id, request.Id), default);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(AccountStatus.PendingVerification);
        request.StatusChangedBy.Should().Be(_admin.Id);
        request.StatusChangedAt.Should().Be(Later);
        _requests.Verify(r => r.TryDecideAsync(request, AccountStatus.PendingApproval, It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.EnqueueAsync(request.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QueuesTheLinkOnlyAfterTheDecisionIsSaved()
    {
        User request = Target(UserBuilder.Requested());
        List<string> order = [];
        _requests
            .Setup(r => r.TryDecideAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("saved"))
            .ReturnsAsync(true);
        _dispatcher
            .Setup(d => d.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("queued"))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new ApproveRegistrationCommand(_admin.Id, request.Id), default);

        order.Should().Equal("saved", "queued");
    }

    [Fact]
    public async Task Handle_WhenAnotherDecisionWonTheRace_FailsWithInvalidTransitionAndSendsNothing()
    {
        User request = Target(UserBuilder.Requested());
        _requests
            .Setup(r => r.TryDecideAsync(It.IsAny<User>(), It.IsAny<AccountStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(new ApproveRegistrationCommand(_admin.Id, request.Id), default);

        result.Error.Should().Be(UserErrors.InvalidTransition);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountStatus.Rejected)]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Active)]
    public async Task Handle_ForARequestThatIsNotPending_FailsWithInvalidTransitionAndChangesNothing(AccountStatus status)
    {
        User user = Target(UserBuilder.InStatus(status, UserRole.Manager, "alvo@example.com"));

        Result result = await _handler.Handle(new ApproveRegistrationCommand(_admin.Id, user.Id), default);

        result.Error.Should().Be(UserErrors.InvalidTransition);
        user.Status.Should().Be(status);
        _requests.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownRequest_FailsWithNotFound()
    {
        Result result = await _handler.Handle(new ApproveRegistrationCommand(_admin.Id, Guid.CreateVersion7()), default);

        result.Error.Should().Be(RegistrationRequestErrors.NotFound);
        _requests.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    public async Task Handle_ForAnActorWhoIsNotAnActiveSuperAdmin_IsForbiddenAndChangesNothing(UserRole role, AccountStatus status)
    {
        User actor = UserBuilder.InStatus(status, role, "ator@example.com");
        _accounts.Setup(a => a.FindByIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        User request = Target(UserBuilder.Requested());

        Result result = await _handler.Handle(new ApproveRegistrationCommand(actor.Id, request.Id), default);

        result.Error.Should().Be(RegistrationRequestErrors.Forbidden);
        request.Status.Should().Be(AccountStatus.PendingApproval);
        _requests.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownActor_IsForbidden()
    {
        User request = Target(UserBuilder.Requested());

        Result result = await _handler.Handle(new ApproveRegistrationCommand(Guid.CreateVersion7(), request.Id), default);

        result.Error.Should().Be(RegistrationRequestErrors.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenTheTargetIsNotAManager_IsForbidden()
    {
        User resident = Target(UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, "morador@example.com"));

        Result result = await _handler.Handle(new ApproveRegistrationCommand(_admin.Id, resident.Id), default);

        result.Error.Should().Be(RegistrationRequestErrors.Forbidden);
        _requests.VerifyNoOtherCalls();
    }

    private User Target(User user)
    {
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
