using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Registration.RegisterManager;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class RegisterManagerCommandHandlerTests
{
    private const string Address = "sindica@example.com";
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailVerifications> _verifications = new();
    private readonly Mock<IEmailVerificationDispatcher> _dispatcher = new();
    private readonly Mock<IClock> _clock = new();
    private readonly RegisterManagerCommandHandler _handler;

    public RegisterManagerCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _handler = new RegisterManagerCommandHandler(_accounts.Object, _verifications.Object, _dispatcher.Object, _clock.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData(null)]
    public async Task Handle_WithAnInvalidEmail_FailsWithoutTouchingAnything(string? address)
    {
        Result result = await _handler.Handle(new RegisterManagerCommand(address!), default);

        result.Error.Should().Be(Email.Invalid);
        _accounts.VerifyNoOtherCalls();
        _verifications.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithANewEmail_RegistersAPendingManagerAndQueuesTheLink()
    {
        User? stored = null;
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => stored = u)
            .ReturnsAsync(true);

        Result result = await _handler.Handle(new RegisterManagerCommand(" Sindica@Example.com "), default);

        result.IsSuccess.Should().BeTrue();
        stored!.Email.Value.Should().Be(Address);
        stored.Role.Should().Be(UserRole.Manager);
        stored.Status.Should().Be(AccountStatus.PendingVerification);
        stored.CondominiumId.Should().BeNull();
        _dispatcher.Verify(d => d.EnqueueAsync(stored.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAnotherRegistrationWinsTheRace_SucceedsWithoutQueueingAnything()
    {
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(new RegisterManagerCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithAnOldLink_QueuesANewLink()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        EmailVerificationToken old = EmailVerificationToken.Issue(pending.Id, "old-hash", Now.AddMinutes(-1));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(old);

        Result result = await _handler.Handle(new RegisterManagerCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(d => d.EnqueueAsync(pending.Id, It.IsAny<CancellationToken>()), Times.Once);
        _verifications.Verify(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithoutAnyLink_QueuesANewLink()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync((EmailVerificationToken?)null);

        await _handler.Handle(new RegisterManagerCommand(Address), default);

        _dispatcher.Verify(d => d.EnqueueAsync(pending.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithARecentLink_SucceedsWithoutQueueingAgain()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        EmailVerificationToken recent = EmailVerificationToken.Issue(pending.Id, "recent-hash", Now.AddSeconds(-30));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recent);

        Result result = await _handler.Handle(new RegisterManagerCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_ForAnAccountThatIsNotPending_SucceedsAndDoesNothing(AccountStatus status)
    {
        ExistingUser(status);

        Result result = await _handler.Handle(new RegisterManagerCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _verifications.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    private User ExistingUser(AccountStatus status)
    {
        User user = UserBuilder.InStatus(status, UserRole.Manager, Address);
        _accounts.Setup(a => a.FindByEmailAsync(It.Is<Email>(e => e.Value == Address), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}