using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Identity.Domain;

namespace Urban.UnitTests.Identity.Application;

public class LoginCommandHandlerTests
{
    private const string Password = "senha-correta-123";

    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<ITokenIssuer> _issuer = new();
    private readonly Mock<IRefreshTokenGenerator> _generator = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<IClock> _clock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _generator.Setup(g => g.Generate()).Returns(new RefreshSecret("refresh-value", "refresh-hash"));
        _handler = new LoginCommandHandler(_accounts.Object, _issuer.Object, _generator.Object, _tokens.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsTokensAndStoresOnlyTheRefreshHash()
    {
        User user = UserBuilder.InStatus(AccountStatus.Active);
        GivenCredentialsResult(Result.Success(user));
        _issuer.Setup(i => i.IssueAccessToken(user, Now)).Returns(new AccessToken("access-jwt", Now.AddMinutes(15)));

        Result<AuthTokens> result = await _handler.Handle(new LoginCommand("ana@example.com", Password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-jwt");
        result.Value.AccessTokenExpiresAt.Should().Be(Now.AddMinutes(15));
        result.Value.RefreshToken.Should().Be("refresh-value");
        result.Value.RefreshTokenExpiresAt.Should().Be(Now.AddDays(7));
        _tokens.Verify(
            t => t.AddAsync(
                It.Is<RefreshToken>(r => r.UserId == user.Id && r.TokenHash == "refresh-hash" && r.ExpiresAt == Now.AddDays(7)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithWrongCredentials_FailsWithInvalidCredentialsAndIssuesNothing()
    {
        GivenCredentialsResult(Result.Failure<User>(LoginErrors.InvalidCredentials));

        Result<AuthTokens> result = await _handler.Handle(new LoginCommand("ana@example.com", "outra-senha-1"), CancellationToken.None);

        result.Error.Should().Be(LoginErrors.InvalidCredentials);
        NothingWasIssued();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Handle_WithMalformedEmail_FailsWithTheSameErrorWithoutTouchingTheAccounts(string email)
    {
        Result<AuthTokens> result = await _handler.Handle(new LoginCommand(email, Password), CancellationToken.None);

        result.Error.Should().Be(LoginErrors.InvalidCredentials);
        _accounts.VerifyNoOtherCalls();
        NothingWasIssued();
    }

    [Fact]
    public async Task Handle_WhenTheAccountIsLockedOut_FailsWithLockedOut()
    {
        GivenCredentialsResult(Result.Failure<User>(LoginErrors.LockedOut));

        Result<AuthTokens> result = await _handler.Handle(new LoginCommand("ana@example.com", Password), CancellationToken.None);

        result.Error.Should().Be(LoginErrors.LockedOut);
        NothingWasIssued();
    }

    [Theory]
    [MemberData(nameof(BlockedAccounts.States), MemberType = typeof(BlockedAccounts))]
    public async Task Handle_WithCorrectPasswordOnABlockedAccount_RevealsTheStateAndIssuesNothing(AccountStatus status, string expectedCode)
    {
        GivenCredentialsResult(Result.Success(UserBuilder.InStatus(status)));

        Result<AuthTokens> result = await _handler.Handle(new LoginCommand("ana@example.com", Password), CancellationToken.None);

        result.Error.Code.Should().Be(expectedCode);
        NothingWasIssued();
    }

    private void GivenCredentialsResult(Result<User> outcome) =>
        _accounts
            .Setup(a => a.CheckCredentialsAsync(Email.Create("ana@example.com").Value, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

    private void NothingWasIssued()
    {
        _issuer.VerifyNoOtherCalls();
        _tokens.VerifyNoOtherCalls();
    }
}
