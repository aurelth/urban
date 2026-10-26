using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.PasswordReset.ResetPassword;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ResetPasswordCommandHandlerTests
{
    private const string Password = "Senha-nova-123";
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IPasswordResets> _resets = new();
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ISessionRevocations> _sessions = new();
    private readonly Mock<IClock> _clock = new();
    private readonly List<string> _calls = [];
    private readonly User _user = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager);
    private readonly PasswordResetToken _token;
    private readonly UserCredential _credential;
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _token = PasswordResetToken.Issue(_user.Id, "link-hash", Now.AddMinutes(-10));
        _credential = UserCredential.Create(_user.Id, "old-hash");
        for (int i = 0; i < UserCredential.MaxFailedAttempts; i++)
        {
            _credential.RegisterFailure(Now.AddMinutes(-1));
        }

        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Hash("link-secret")).Returns("link-hash");
        _hasher.Setup(h => h.Hash(Password)).Returns("new-hash");
        _resets.Setup(r => r.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(_token);
        _resets.Setup(r => r.FindCredentialOfUserAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_credential);
        _resets
            .Setup(r => r.ResetAsync(It.IsAny<PasswordResetToken>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("reset"))
            .ReturnsAsync(true);
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _refreshTokens
            .Setup(r => r.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("refresh-revoked"));
        _sessions
            .Setup(s => s.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("access-revoked"));
        _handler = new ResetPasswordCommandHandler(
            _resets.Object, _accounts.Object, _secrets.Object, _hasher.Object, _refreshTokens.Object, _sessions.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WithAValidLinkAndPassword_StoresTheNewHashAndUsesTheLink()
    {
        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.IsSuccess.Should().BeTrue();
        _credential.PasswordHash.Should().Be("new-hash");
        _token.UsedAt.Should().Be(Now);
        _resets.Verify(r => r.ResetAsync(_token, _credential, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnlocksTheAccount()
    {
        _credential.IsLockedOut(Now).Should().BeTrue();

        await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        _credential.IsLockedOut(Now).Should().BeFalse();
        _credential.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Handle_EndsEverySessionOfThatUser_AfterThePasswordWasSaved()
    {
        await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        _refreshTokens.Verify(r => r.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
        _sessions.Verify(s => s.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
        _calls.Should().Equal("reset", "refresh-revoked", "access-revoked");
    }

    [Theory]
    [InlineData("curta1", "identity.password.too_short")]
    [InlineData("somente-letras-aqui", "identity.password.needs_letter_and_digit")]
    public async Task Handle_WithAWeakPassword_FailsBeforeLookingUpTheLink(string password, string code)
    {
        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", password), default);

        result.Error.Code.Should().Be(code);
        _resets.VerifyNoOtherCalls();
        _token.UsedAt.Should().BeNull();
        NothingWasRevoked();
    }

    [Fact]
    public async Task Handle_WithAnUnknownLink_FailsAsInvalid()
    {
        _resets.Setup(r => r.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync((PasswordResetToken?)null);

        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.Error.Should().Be(PasswordResetTokenErrors.Invalid);
        NothingWasSaved();
    }

    [Fact]
    public async Task Handle_WithAnExpiredLink_FailsAsExpired()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_token.ExpiresAt);

        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.Error.Should().Be(PasswordResetTokenErrors.Expired);
        _credential.PasswordHash.Should().Be("old-hash");
        NothingWasSaved();
    }

    [Fact]
    public async Task Handle_WithAnAlreadyUsedLink_FailsAsInvalid()
    {
        _token.Consume(Now.AddMinutes(-5));

        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.Error.Should().Be(PasswordResetTokenErrors.Invalid);
        NothingWasSaved();
    }

    [Fact]
    public async Task Handle_WhenTheUserNoLongerExists_FailsAsInvalid()
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.Error.Should().Be(PasswordResetTokenErrors.Invalid);
        NothingWasSaved();
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_WhenTheAccountIsNotActive_FailsAsInvalid(AccountStatus status)
    {
        User other = UserBuilder.InStatus(status, UserRole.Manager);
        PasswordResetToken token = PasswordResetToken.Issue(other.Id, "link-hash", Now.AddMinutes(-10));
        _resets.Setup(r => r.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _accounts.Setup(a => a.FindByIdAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(other);
        _resets
            .Setup(r => r.FindCredentialOfUserAsync(other.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCredential.Create(other.Id, "old-hash"));

        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.Error.Should().Be(PasswordResetTokenErrors.Invalid);
        NothingWasSaved();
    }

    [Fact]
    public async Task Handle_WhenTheAccountHasNoCredential_FailsAsInvalid()
    {
        _resets.Setup(r => r.FindCredentialOfUserAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((UserCredential?)null);

        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.Error.Should().Be(PasswordResetTokenErrors.Invalid);
        NothingWasSaved();
    }

    [Fact]
    public async Task Handle_WhenAnotherRequestUsedTheLinkFirst_FailsAsInvalid_AndEndsNoSession()
    {
        _resets
            .Setup(r => r.ResetAsync(It.IsAny<PasswordResetToken>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(new ResetPasswordCommand("link-secret", Password), default);

        result.Error.Should().Be(PasswordResetTokenErrors.Invalid);
        NothingWasRevoked();
    }

    private void NothingWasSaved()
    {
        _resets.Verify(r => r.ResetAsync(It.IsAny<PasswordResetToken>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
        NothingWasRevoked();
    }

    private void NothingWasRevoked()
    {
        _refreshTokens.VerifyNoOtherCalls();
        _sessions.VerifyNoOtherCalls();
    }
}