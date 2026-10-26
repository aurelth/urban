using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.PasswordReset.SendPasswordReset;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class SendPasswordResetCommandHandlerTests
{
    private const string Address = "sindica@example.com";
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IPasswordResets> _resets = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IPasswordResetNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();
    private readonly List<string> _calls = [];
    private readonly SendPasswordResetCommandHandler _handler;

    public SendPasswordResetCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Generate()).Returns(new GeneratedSecret("link-secret", "link-hash"));
        _resets
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("stored"));
        _notifier
            .Setup(n => n.SendAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("sent"));
        _handler = new SendPasswordResetCommandHandler(_accounts.Object, _resets.Object, _secrets.Object, _notifier.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_ForAnActiveAccountWithoutAnyLink_StoresOnlyTheHashAndSendsTheSecret()
    {
        User user = ActiveUser();
        PasswordResetToken? added = null;
        _resets
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => added = t);

        Result result = await _handler.Handle(new SendPasswordResetCommand(user.Id), default);

        result.IsSuccess.Should().BeTrue();
        added!.UserId.Should().Be(user.Id);
        added.TokenHash.Should().Be("link-hash");
        added.CreatedAt.Should().Be(Now);
        _notifier.Verify(n => n.SendAsync(It.Is<Email>(e => e.Value == Address), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StoresTheLinkBeforeSendingIt()
    {
        User user = ActiveUser();

        await _handler.Handle(new SendPasswordResetCommand(user.Id), default);

        _calls.Should().Equal("stored", "sent");
    }

    [Fact]
    public async Task Handle_ForAnActiveAccountWithAnOldLink_IssuesANewOne()
    {
        User user = ActiveUser();
        PasswordResetToken old = PasswordResetToken.Issue(user.Id, "old-hash", Now.AddMinutes(-1));
        _resets.Setup(r => r.FindLatestOfUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(old);

        await _handler.Handle(new SendPasswordResetCommand(user.Id), default);

        _resets.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.SendAsync(It.IsAny<Email>(), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenALinkWasIssuedAMomentAgo_SendsNothing()
    {
        User user = ActiveUser();
        PasswordResetToken recent = PasswordResetToken.Issue(user.Id, "recent-hash", Now.AddSeconds(-30));
        _resets.Setup(r => r.FindLatestOfUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recent);

        Result result = await _handler.Handle(new SendPasswordResetCommand(user.Id), default);

        result.IsSuccess.Should().BeTrue();
        _resets.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_SucceedsAndSendsNothing()
    {
        Result result = await _handler.Handle(new SendPasswordResetCommand(Guid.CreateVersion7()), default);

        result.IsSuccess.Should().BeTrue();
        _resets.VerifyNoOtherCalls();
        _notifier.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_ForAnAccountThatIsNoLongerActive_SucceedsAndSendsNothing(AccountStatus status)
    {
        User user = UserBuilder.InStatus(status, UserRole.Manager, Address);
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        Result result = await _handler.Handle(new SendPasswordResetCommand(user.Id), default);

        result.IsSuccess.Should().BeTrue();
        _resets.VerifyNoOtherCalls();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheProviderFails_LetsTheFailureReachTheRetryMechanism()
    {
        User user = ActiveUser();
        _notifier
            .Setup(n => n.SendAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> handle = () => _handler.Handle(new SendPasswordResetCommand(user.Id), default);

        await handle.Should().ThrowAsync<EmailDeliveryException>();
    }

    private User ActiveUser()
    {
        User user = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager, Address);
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}