using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Registration.CheckEmailLink;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Consent;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

/// <summary>A tela de confirmação confere o link assim que abre, sem gastá-lo: quem tem um convite vencido descobre antes de digitar a senha.</summary>
public class CheckEmailLinkQueryHandlerTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IEmailVerifications> _verifications = new();
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IClock> _clock = new();
    private readonly User _user = UserBuilder.InStatus(AccountStatus.PendingVerification, UserRole.Resident);
    private readonly CheckEmailLinkQueryHandler _handler;

    public CheckEmailLinkQueryHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Hash("link-secret")).Returns("link-hash");
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _handler = new CheckEmailLinkQueryHandler(_verifications.Object, _accounts.Object, _secrets.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WithAValidLinkOfAPendingAccount_Succeeds_AndSpendsNothing()
    {
        EmailVerificationToken token = Link(issuedAt: Now.AddHours(-1));

        Result result = await _handler.Handle(new CheckEmailLinkQuery("link-secret"), default);

        result.IsSuccess.Should().BeTrue();
        token.UsedAt.Should().BeNull();
        _user.Status.Should().Be(AccountStatus.PendingVerification);
        _verifications.Verify(
            v => v.ConfirmAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<User>(), It.IsAny<UserCredential>(), It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnUnknownLink_IsInvalid()
    {
        Result result = await _handler.Handle(new CheckEmailLinkQuery("link-secret"), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }

    [Fact]
    public async Task Handle_WithAUsedLink_IsInvalid()
    {
        EmailVerificationToken token = Link(issuedAt: Now.AddHours(-1));
        token.Consume(Now.AddMinutes(-5));

        Result result = await _handler.Handle(new CheckEmailLinkQuery("link-secret"), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }

    [Fact]
    public async Task Handle_WithAnExpiredLink_IsExpired()
    {
        Link(issuedAt: Now.AddHours(-25));

        Result result = await _handler.Handle(new CheckEmailLinkQuery("link-secret"), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Expired);
    }

    [Fact]
    public async Task Handle_WithAnInvitationStillInsideThe72Hours_Succeeds()
    {
        Link(issuedAt: Now.AddHours(-60), invitation: true);

        Result result = await _handler.Handle(new CheckEmailLinkQuery("link-secret"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deactivated)]
    public async Task Handle_WithALinkOfAnAccountThatIsNoLongerPending_IsInvalid(AccountStatus status)
    {
        User other = UserBuilder.InStatus(status, UserRole.Resident);
        EmailVerificationToken token = EmailVerificationToken.Issue(other.Id, "link-hash", Now.AddMinutes(-5));
        _verifications.Setup(v => v.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _accounts.Setup(a => a.FindByIdAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(other);

        Result result = await _handler.Handle(new CheckEmailLinkQuery("link-secret"), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }

    [Fact]
    public async Task Handle_WithALinkWhoseAccountIsGone_IsInvalid()
    {
        EmailVerificationToken token = EmailVerificationToken.Issue(Guid.CreateVersion7(), "link-hash", Now.AddMinutes(-5));
        _verifications.Setup(v => v.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        Result result = await _handler.Handle(new CheckEmailLinkQuery("link-secret"), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }

    [Fact]
    public async Task Handle_WithoutAToken_IsInvalid()
    {
        Result result = await _handler.Handle(new CheckEmailLinkQuery(string.Empty), default);

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }

    private EmailVerificationToken Link(DateTimeOffset issuedAt, bool invitation = false)
    {
        EmailVerificationToken token = invitation
            ? EmailVerificationToken.IssueInvitation(_user.Id, "link-hash", issuedAt)
            : EmailVerificationToken.Issue(_user.Id, "link-hash", issuedAt);
        _verifications.Setup(v => v.FindByHashAsync("link-hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        return token;
    }
}
