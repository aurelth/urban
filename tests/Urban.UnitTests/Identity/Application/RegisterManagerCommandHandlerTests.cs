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
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IEmailVerificationNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();
    private readonly RegisterManagerCommandHandler _handler;

    public RegisterManagerCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Generate()).Returns(new GeneratedSecret("link-secret", "link-hash"));
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _handler = new RegisterManagerCommandHandler(_accounts.Object, _verifications.Object, _secrets.Object, _notifier.Object, _clock.Object);
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
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithANewEmail_RegistersAPendingManagerAndSendsTheLink()
    {
        User? stored = null;
        EmailVerificationToken? storedToken = null;
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback<User, EmailVerificationToken, CancellationToken>((u, t, _) => (stored, storedToken) = (u, t))
            .ReturnsAsync(true);

        Result result = await _handler.Handle(new RegisterManagerCommand(" Sindica@Example.com "), default);

        result.IsSuccess.Should().BeTrue();
        stored!.Email.Value.Should().Be(Address);
        stored.Role.Should().Be(UserRole.Manager);
        stored.Status.Should().Be(AccountStatus.PendingVerification);
        stored.CondominiumId.Should().BeNull();
        storedToken!.UserId.Should().Be(stored.Id);
        storedToken.TokenHash.Should().Be("link-hash");
        _notifier.Verify(n => n.SendAsync(It.Is<Email>(e => e.Value == Address), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAnotherRegistrationWinsTheRace_SucceedsWithoutSendingAnything()
    {
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(new RegisterManagerCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithAnOldLink_IssuesAndSendsANewLink()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        EmailVerificationToken old = EmailVerificationToken.Issue(pending.Id, "old-hash", Now.AddMinutes(-1));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(old);
        EmailVerificationToken? added = null;
        _verifications
            .Setup(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailVerificationToken, CancellationToken>((t, _) => added = t);

        Result result = await _handler.Handle(new RegisterManagerCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        added!.UserId.Should().Be(pending.Id);
        added.TokenHash.Should().Be("link-hash");
        _notifier.Verify(n => n.SendAsync(It.Is<Email>(e => e.Value == Address), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
        _verifications.Verify(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithoutAnyLink_IssuesANewLink()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync((EmailVerificationToken?)null);

        await _handler.Handle(new RegisterManagerCommand(Address), default);

        _verifications.Verify(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.SendAsync(It.IsAny<Email>(), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithARecentLink_SucceedsWithoutSendingAgain()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        EmailVerificationToken recent = EmailVerificationToken.Issue(pending.Id, "recent-hash", Now.AddSeconds(-30));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recent);

        Result result = await _handler.Handle(new RegisterManagerCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _verifications.Verify(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.VerifyNoOtherCalls();
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
        _notifier.VerifyNoOtherCalls();
    }

    private User ExistingUser(AccountStatus status)
    {
        User user = UserBuilder.InStatus(status, UserRole.Manager, Address);
        _accounts.Setup(a => a.FindByEmailAsync(It.Is<Email>(e => e.Value == Address), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}