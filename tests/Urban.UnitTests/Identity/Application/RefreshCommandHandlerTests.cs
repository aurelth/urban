using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Refresh;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class RefreshCommandHandlerTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<ITokenIssuer> _issuer = new();
    private readonly Mock<ISecretGenerator> _generator = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<ISessionRevocations> _sessions = new();
    private readonly Mock<IClock> _clock = new();
    private readonly User _user = UserBuilder.InStatus(AccountStatus.Active);
    private readonly RefreshToken _stored;
    private readonly RefreshCommandHandler _handler;

    public RefreshCommandHandlerTests()
    {
        _stored = RefreshToken.Issue(_user.Id, "old-hash", Now.AddHours(-1));
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _generator.Setup(g => g.Hash("old-value")).Returns("old-hash");
        _generator.Setup(g => g.Generate()).Returns(new GeneratedSecret("new-value", "new-hash"));
        _tokens.Setup(t => t.FindByHashAsync("old-hash", It.IsAny<CancellationToken>())).ReturnsAsync(_stored);
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _handler = new RefreshCommandHandler(
            _accounts.Object, _issuer.Object, _generator.Object, _tokens.Object, _sessions.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WithAnActiveToken_RotatesItAndReturnsNewTokens()
    {
        _issuer.Setup(i => i.IssueAccessToken(_user, Now)).Returns(new AccessToken("new-access", Now.AddMinutes(15)));

        Result<AuthTokens> result = await Send("old-value");

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
        result.Value.AccessTokenExpiresAt.Should().Be(Now.AddMinutes(15));
        result.Value.RefreshToken.Should().Be("new-value");
        result.Value.RefreshTokenExpiresAt.Should().Be(Now.AddDays(7));
        _stored.RevokedAt.Should().Be(Now);
        _tokens.Verify(
            t => t.ReplaceAsync(
                _stored,
                It.Is<RefreshToken>(r => r.Id == _stored.ReplacedById && r.TokenHash == "new-hash" && r.UserId == _user.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAnUnknownToken_FailsWithInvalidSessionAndChangesNothing()
    {
        _tokens.Setup(t => t.FindByHashAsync("old-hash", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        Result<AuthTokens> result = await Send("old-value");

        result.Error.Should().Be(RefreshErrors.InvalidSession);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WhenTheUserNoLongerExists_FailsWithInvalidSessionAndChangesNothing()
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result<AuthTokens> result = await Send("old-value");

        result.Error.Should().Be(RefreshErrors.InvalidSession);
        NothingWasChanged();
    }

    [Theory]
    [MemberData(nameof(BlockedAccounts.States), MemberType = typeof(BlockedAccounts))]
    public async Task Handle_WhenTheAccountCannotSignInAnymore_FailsWithTheStateAndLeavesTheTokenUntouched(
        AccountStatus status,
        string expectedCode)
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(UserBuilder.InStatus(status));

        Result<AuthTokens> result = await Send("old-value");

        result.Error.Code.Should().Be(expectedCode);
        _stored.RevokedAt.Should().BeNull();
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WithAnExpiredToken_FailsWithExpiredAndChangesNothing()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now.AddDays(8));

        Result<AuthTokens> result = await Send("old-value");

        result.Error.Should().Be(RefreshTokenErrors.Expired);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WithAReusedToken_RevokesEveryTokenAndEverySessionOfTheUser()
    {
        _stored.Revoke(Now.AddMinutes(-30));

        Result<AuthTokens> result = await Send("old-value");

        result.Error.Should().Be(RefreshTokenErrors.Reused);
        _tokens.Verify(t => t.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
        _sessions.Verify(s => s.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.ReplaceAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _issuer.VerifyNoOtherCalls();
    }

    private Task<Result<AuthTokens>> Send(string refreshToken) =>
        _handler.Handle(new RefreshCommand(refreshToken), CancellationToken.None);

    private void NothingWasChanged()
    {
        _tokens.Verify(t => t.ReplaceAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokens.Verify(t => t.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.VerifyNoOtherCalls();
        _issuer.VerifyNoOtherCalls();
    }
}
