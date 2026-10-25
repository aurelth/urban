using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Registration.ConfirmEmail;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ConfirmEmailCommandHandlerTests
{
    private const string Password = "Senha-segura-123";
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IEmailVerifications> _verifications = new();
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IClock> _clock = new();
    private readonly User _user = UserBuilder.InStatus(AccountStatus.PendingVerification, UserRole.Manager);
    private readonly EmailVerificationToken _token;
    private readonly ConfirmEmailCommandHandler _handler;

    public ConfirmEmailCommandHandlerTests()
    {
        _token = EmailVerificationToken.Issue(_user.Id, "link-hash", Now.AddHours(-1));
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Hash("link-secret")).Returns("link-hash");
        _hasher.Setup(h => h.Hash(Password)).Returns("bcrypt-hash");
        _verifications.Setup(v => v.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(_token);
        _verifications
            .Setup(v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _handler = new ConfirmEmailCommandHandler(_verifications.Object, _accounts.Object, _secrets.Object, _hasher.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WithAValidLinkAndPassword_ActivatesTheAccountAndStoresTheHashedPassword()
    {
        UserCredential? stored = null;
        _verifications
            .Setup(v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()))
            .Callback<EmailVerificationToken, User, UserCredential, CancellationToken>((_, _, c, _) => stored = c)
            .ReturnsAsync(true);

        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", Password), default);

        result.IsSuccess.Should().BeTrue();
        _user.Status.Should().Be(AccountStatus.Active);
        _token.UsedAt.Should().Be(Now);
        stored!.UserId.Should().Be(_user.Id);
        stored.PasswordHash.Should().Be("bcrypt-hash");
        _verifications.Verify(v => v.ConfirmAsync(_token, _user, stored, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("curta1", "identity.password.too_short")]
    [InlineData("somente-letras-aqui", "identity.password.needs_letter_and_digit")]
    public async Task Handle_WithAWeakPassword_FailsBeforeLookingUpTheLink(string password, string code)
    {
        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", password), default);

        result.Error.Code.Should().Be(code);
        _verifications.VerifyNoOtherCalls();
        _token.UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAnUnknownLink_FailsAsInvalid()
    {
        _verifications.Setup(v => v.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync((EmailVerificationToken?)null);

        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", Password), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
        _verifications.Verify(v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnExpiredLink_FailsAsExpired()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_token.ExpiresAt);

        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", Password), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Expired);
        _user.Status.Should().Be(AccountStatus.PendingVerification);
        _verifications.Verify(v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnAlreadyUsedLink_FailsAsInvalid()
    {
        _token.Consume(Now.AddMinutes(-5));

        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", Password), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }

    [Fact]
    public async Task Handle_WhenTheUserNoLongerExists_FailsAsInvalid()
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", Password), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
        _verifications.Verify(v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheAccountIsNotPending_FailsAsInvalid()
    {
        _user.ConfirmEmail(Now.AddMinutes(-10));

        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", Password), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
        _verifications.Verify(v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnotherRequestUsedTheLinkFirst_FailsAsInvalid()
    {
        _verifications
            .Setup(v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(new ConfirmEmailCommand("link-secret", Password), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }
}