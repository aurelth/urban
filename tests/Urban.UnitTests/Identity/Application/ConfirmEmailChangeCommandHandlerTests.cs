using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.EmailChange.ConfirmEmailChange;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ConfirmEmailChangeCommandHandlerTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly Email NewEmail = Email.Create("nova@example.com").Value;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailChanges> _changes = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ISessionRevocations> _sessions = new();
    private readonly Mock<ISecurityNoticeDispatcher> _notices = new();
    private readonly Mock<IClock> _clock = new();
    private readonly List<string> _calls = [];
    private readonly User _user = UserBuilder.EnrolledManager("Ana", "sindica@example.com");
    private readonly EmailChangeToken _token;
    private readonly ConfirmEmailChangeCommandHandler _handler;

    public ConfirmEmailChangeCommandHandlerTests()
    {
        _token = EmailChangeToken.Issue(_user.Id, NewEmail, "link-hash", Now.AddMinutes(-10));
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Hash("link-secret")).Returns("link-hash");
        _changes.Setup(c => c.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(_token);
        _changes
            .Setup(c => c.ConfirmAsync(_token, _user, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("confirmed"))
            .ReturnsAsync(true);
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _refreshTokens
            .Setup(r => r.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("refresh-revoked"))
            .Returns(Task.CompletedTask);
        _sessions
            .Setup(s => s.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("access-revoked"))
            .Returns(Task.CompletedTask);
        _handler = new ConfirmEmailChangeCommandHandler(
            _accounts.Object, _changes.Object, _secrets.Object, _refreshTokens.Object, _sessions.Object, _notices.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WithAValidLink_SwapsTheAddressAndUsesTheLink()
    {
        Result result = await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        result.IsSuccess.Should().BeTrue();
        _user.Email.Should().Be(NewEmail);
        _token.UsedAt.Should().Be(Now);
        _changes.Verify(c => c.ConfirmAsync(_token, _user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EndsEverySession_AfterTheSwapWasSaved()
    {
        await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        _calls.Should().Equal("confirmed", "refresh-revoked", "access-revoked");
    }

    [Fact]
    public async Task Handle_TellsThePreviousAddressAboutTheChange()
    {
        Email previous = _user.Email;

        await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        _notices.Verify(n => n.EnqueueAsync(SecurityNotice.EmailChanged, previous, NewEmail, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAnUnknownLink_FailsAsInvalid()
    {
        _changes.Setup(c => c.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync((EmailChangeToken?)null);

        Result result = await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        result.Error.Should().Be(EmailChangeTokenErrors.Invalid);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WithAnExpiredLink_FailsAsExpired()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_token.ExpiresAt);

        Result result = await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        result.Error.Should().Be(EmailChangeTokenErrors.Expired);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WithAnAlreadyUsedLink_FailsAsInvalid()
    {
        _token.Consume(Now.AddMinutes(-5));

        Result result = await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        result.Error.Should().Be(EmailChangeTokenErrors.Invalid);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WhenTheAddressWasTakenInTheMeantime_FailsAsInvalidAndChangesNothing()
    {
        _changes.Setup(c => c.ConfirmAsync(_token, _user, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Result result = await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        result.Error.Should().Be(EmailChangeTokenErrors.Invalid);
        _refreshTokens.Verify(r => r.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(s => s.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _notices.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheUserNoLongerExists_FailsAsInvalid()
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        result.Error.Should().Be(EmailChangeTokenErrors.Invalid);
        NothingWasChanged();
    }

    [Theory]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_WhenTheAccountIsNotActive_FailsAsInvalid(AccountStatus status)
    {
        User other = UserBuilder.InStatus(status, UserRole.Manager);
        EmailChangeToken token = EmailChangeToken.Issue(other.Id, NewEmail, "link-hash", Now.AddMinutes(-10));
        _changes.Setup(c => c.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _accounts.Setup(a => a.FindByIdAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(other);

        Result result = await _handler.Handle(new ConfirmEmailChangeCommand("link-secret"), default);

        result.Error.Should().Be(EmailChangeTokenErrors.Invalid);
        NothingWasChanged();
    }

    private void NothingWasChanged()
    {
        _changes.Verify(c => c.ConfirmAsync(It.IsAny<EmailChangeToken>(), It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.Verify(r => r.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _notices.VerifyNoOtherCalls();
    }
}
