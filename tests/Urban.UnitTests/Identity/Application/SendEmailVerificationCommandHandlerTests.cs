using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Registration.SendEmailVerification;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class SendEmailVerificationCommandHandlerTests
{
    private const string Address = "sindica@example.com";
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailVerifications> _verifications = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IEmailVerificationNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();
    private readonly List<string> _calls = [];
    private readonly SendEmailVerificationCommandHandler _handler;

    public SendEmailVerificationCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Generate()).Returns(new GeneratedSecret("link-secret", "link-hash"));
        _verifications
            .Setup(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("stored"));
        _notifier
            .Setup(n => n.SendAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("sent"));
        _handler = new SendEmailVerificationCommandHandler(_accounts.Object, _verifications.Object, _secrets.Object, _notifier.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithoutAnyLink_StoresOnlyTheHashAndSendsTheSecret()
    {
        User pending = PendingUser();
        EmailVerificationToken? added = null;
        _verifications
            .Setup(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailVerificationToken, CancellationToken>((t, _) => added = t);

        Result result = await _handler.Handle(new SendEmailVerificationCommand(pending.Id), default);

        result.IsSuccess.Should().BeTrue();
        added!.UserId.Should().Be(pending.Id);
        added.TokenHash.Should().Be("link-hash");
        _notifier.Verify(n => n.SendAsync(It.Is<Email>(e => e.Value == Address), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StoresTheLinkBeforeSendingIt()
    {
        User pending = PendingUser();

        await _handler.Handle(new SendEmailVerificationCommand(pending.Id), default);

        _calls.Should().Equal("stored", "sent");
    }

    [Fact]
    public async Task Handle_ForAPendingAccountWithAnOldLink_IssuesANewOne()
    {
        User pending = PendingUser();
        EmailVerificationToken old = EmailVerificationToken.Issue(pending.Id, "old-hash", Now.AddMinutes(-1));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(old);

        await _handler.Handle(new SendEmailVerificationCommand(pending.Id), default);

        _verifications.Verify(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.SendAsync(It.IsAny<Email>(), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenALinkWasIssuedAMomentAgo_SendsNothing()
    {
        User pending = PendingUser();
        EmailVerificationToken recent = EmailVerificationToken.Issue(pending.Id, "recent-hash", Now.AddSeconds(-30));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recent);

        Result result = await _handler.Handle(new SendEmailVerificationCommand(pending.Id), default);

        result.IsSuccess.Should().BeTrue();
        _verifications.Verify(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_SucceedsAndSendsNothing()
    {
        Result result = await _handler.Handle(new SendEmailVerificationCommand(Guid.CreateVersion7()), default);

        result.IsSuccess.Should().BeTrue();
        _verifications.VerifyNoOtherCalls();
        _notifier.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_ForAnAccountThatIsNoLongerPending_SucceedsAndSendsNothing(AccountStatus status)
    {
        User user = UserBuilder.InStatus(status, UserRole.Manager, Address);
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        Result result = await _handler.Handle(new SendEmailVerificationCommand(user.Id), default);

        result.IsSuccess.Should().BeTrue();
        _verifications.VerifyNoOtherCalls();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheProviderFails_LetsTheFailureReachTheRetryMechanism()
    {
        User pending = PendingUser();
        _notifier
            .Setup(n => n.SendAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> handle = () => _handler.Handle(new SendEmailVerificationCommand(pending.Id), default);

        await handle.Should().ThrowAsync<EmailDeliveryException>();
    }

    private User PendingUser()
    {
        User user = UserBuilder.InStatus(AccountStatus.PendingVerification, UserRole.Manager, Address);
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}