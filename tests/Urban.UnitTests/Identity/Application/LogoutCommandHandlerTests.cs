using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication.Logout;
using Urban.Identity.Domain.Tokens;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class LogoutCommandHandlerTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly DateTimeOffset AccessTokenExpiresAt = Now.AddMinutes(10);

    private readonly Mock<IRefreshTokenGenerator> _generator = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<ISessionRevocations> _sessions = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly RefreshToken _stored;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _stored = RefreshToken.Issue(_userId, "stored-hash", Now.AddHours(-1));
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _generator.Setup(g => g.Hash("stored-value")).Returns("stored-hash");
        _tokens.Setup(t => t.FindByHashAsync("stored-hash", It.IsAny<CancellationToken>())).ReturnsAsync(_stored);
        _handler = new LogoutCommandHandler(_generator.Object, _tokens.Object, _sessions.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_RevokesTheAccessTokenAndTheRefreshToken()
    {
        Result result = await Send("stored-value");

        result.IsSuccess.Should().BeTrue();
        _sessions.Verify(s => s.RevokeAccessTokenAsync("jti-1", AccessTokenExpiresAt, It.IsAny<CancellationToken>()), Times.Once);
        _stored.RevokedAt.Should().Be(Now);
        _tokens.Verify(t => t.UpdateAsync(_stored, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithoutARefreshToken_StillRevokesTheAccessToken(string? refreshToken)
    {
        Result result = await Send(refreshToken);

        result.IsSuccess.Should().BeTrue();
        _sessions.Verify(s => s.RevokeAccessTokenAsync("jti-1", AccessTokenExpiresAt, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithAnUnknownRefreshToken_SucceedsWithoutRevealingItAndRevokesTheAccessToken()
    {
        _tokens.Setup(t => t.FindByHashAsync("stored-hash", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        Result result = await Send("stored-value");

        result.IsSuccess.Should().BeTrue();
        _sessions.Verify(s => s.RevokeAccessTokenAsync("jti-1", AccessTokenExpiresAt, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithARefreshTokenOfAnotherUser_LeavesItUntouched()
    {
        Result result = await _handler.Handle(
            new LogoutCommand(Guid.CreateVersion7(), "jti-1", AccessTokenExpiresAt, "stored-value"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _stored.RevokedAt.Should().BeNull();
        _tokens.Verify(t => t.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task<Result> Send(string? refreshToken) =>
        _handler.Handle(new LogoutCommand(_userId, "jti-1", AccessTokenExpiresAt, refreshToken), CancellationToken.None);
}
