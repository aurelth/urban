using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.PasswordChange;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ChangePasswordCommandHandlerTests
{
    private const string Current = "Senha-atual-123";
    private const string New = "Senha-nova-456";

    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ISessionRevocations> _sessions = new();
    private readonly Mock<ISecurityNoticeDispatcher> _notices = new();
    private readonly Mock<IClock> _clock = new();
    private readonly List<string> _calls = [];
    private readonly User _user = UserBuilder.EnrolledManager("Ana", "sindica@example.com");
    private readonly UserCredential _credential;
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _credential = UserCredential.Create(_user.Id, "old-hash");
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _hasher.Setup(h => h.Hash(New)).Returns("new-hash");
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _accounts
            .Setup(a => a.CheckCredentialsAsync(_user.Email, Current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_user));
        _accounts.Setup(a => a.FindCredentialAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_credential);
        _accounts
            .Setup(a => a.SaveCredentialAsync(_credential, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("saved"))
            .Returns(Task.CompletedTask);
        _refreshTokens
            .Setup(r => r.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("refresh-revoked"))
            .Returns(Task.CompletedTask);
        _sessions
            .Setup(s => s.RevokeAllOfUserAsync(_user.Id, Now, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("access-revoked"))
            .Returns(Task.CompletedTask);
        _handler = new ChangePasswordCommandHandler(_accounts.Object, _hasher.Object, _refreshTokens.Object, _sessions.Object, _notices.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WithTheRightCurrentPassword_StoresTheHashOfTheNewOne()
    {
        Result result = await _handler.Handle(Command(), default);

        result.IsSuccess.Should().BeTrue();
        _credential.PasswordHash.Should().Be("new-hash");
        _accounts.Verify(a => a.SaveCredentialAsync(_credential, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAWrongCurrentPassword_FailsAsPasswordIncorrect_AndNothingIsSaved()
    {
        _accounts
            .Setup(a => a.CheckCredentialsAsync(_user.Email, "errada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<User>(LoginErrors.InvalidCredentials));

        Result result = await _handler.Handle(Command() with { CurrentPassword = "errada" }, default);

        result.Error.Should().Be(UserErrors.PasswordIncorrect);
        _credential.PasswordHash.Should().Be("old-hash");
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_ForALockedAccount_KeepsTheLockoutError()
    {
        _accounts
            .Setup(a => a.CheckCredentialsAsync(_user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<User>(LoginErrors.LockedOut));

        Result result = await _handler.Handle(Command(), default);

        result.Error.Should().Be(LoginErrors.LockedOut);
        NothingWasChanged();
    }

    [Theory]
    [InlineData("curta1", "identity.password.too_short")]
    [InlineData("somente-letras-aqui", "identity.password.needs_letter_and_digit")]
    [InlineData(null, "identity.password.too_short")]
    public async Task Handle_WithAWeakNewPassword_FailsWithThePolicyError_WithoutSpendingAnAttemptOnTheCurrentOne(string? weak, string code)
    {
        Result result = await _handler.Handle(Command() with { NewPassword = weak }, default);

        result.Error.Code.Should().Be(code);
        _accounts.Verify(a => a.CheckCredentialsAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WithTheSamePasswordAsTheCurrentOne_Fails_WithoutSpendingAnAttempt()
    {
        Result result = await _handler.Handle(Command() with { NewPassword = Current }, default);

        result.Error.Should().Be(UserErrors.PasswordUnchanged);
        _accounts.Verify(a => a.CheckCredentialsAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_EndsEverySessionOfThatUser_AfterThePasswordWasSaved()
    {
        await _handler.Handle(Command(), default);

        _calls.Should().Equal("saved", "refresh-revoked", "access-revoked");
    }

    [Fact]
    public async Task Handle_QueuesTheSecurityNoticeToTheCurrentAddress()
    {
        await _handler.Handle(Command(), default);

        _notices.Verify(n => n.EnqueueAsync(SecurityNotice.PasswordChanged, _user.Email, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_FailsAsUnauthenticated()
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _handler.Handle(Command(), default);

        result.Error.Should().Be(SessionErrors.Unauthenticated);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_ForAnAccountThatCannotSignIn_FailsAndChangesNothing()
    {
        User suspended = UserBuilder.InStatus(Urban.Identity.Domain.Users.AccountStatus.Suspended, Urban.Identity.Domain.Access.UserRole.Manager);
        _accounts.Setup(a => a.FindByIdAsync(suspended.Id, It.IsAny<CancellationToken>())).ReturnsAsync(suspended);

        Result result = await _handler.Handle(Command() with { UserId = suspended.Id }, default);

        result.Error.Should().Be(UserErrors.AccountSuspended);
        NothingWasChanged();
    }

    [Fact]
    public async Task Handle_WhenTheAccountHasNoCredential_FailsAsUnauthenticated()
    {
        _accounts.Setup(a => a.FindCredentialAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((UserCredential?)null);

        Result result = await _handler.Handle(Command(), default);

        result.Error.Should().Be(SessionErrors.Unauthenticated);
        NothingWasChanged();
    }

    [Fact]
    public void TheCommand_NeverPrintsThePasswords()
    {
        string text = Command().ToString();

        text.Should().NotContain(Current).And.NotContain(New);
    }

    private ChangePasswordCommand Command() => new(_user.Id, Current, New);

    private void NothingWasChanged()
    {
        _accounts.Verify(a => a.SaveCredentialAsync(It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.Verify(r => r.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(s => s.RevokeAllOfUserAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _notices.VerifyNoOtherCalls();
    }
}
