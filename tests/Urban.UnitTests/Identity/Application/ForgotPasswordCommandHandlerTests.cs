using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.PasswordReset.ForgotPassword;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ForgotPasswordCommandHandlerTests
{
    private const string Address = "sindica@example.com";
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IPasswordResets> _resets = new();
    private readonly Mock<IPasswordResetDispatcher> _dispatcher = new();
    private readonly Mock<IClock> _clock = new();
    private readonly ForgotPasswordCommandHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _handler = new ForgotPasswordCommandHandler(_accounts.Object, _resets.Object, _dispatcher.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_ForAnActiveAccount_QueuesTheEmailForThatUser()
    {
        User user = AccountIn(AccountStatus.Active);

        Result result = await _handler.Handle(new ForgotPasswordCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(d => d.EnqueueAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForAnActiveAccountWithAnOldLink_QueuesTheEmail()
    {
        User user = AccountIn(AccountStatus.Active);
        PasswordResetToken old = PasswordResetToken.Issue(user.Id, "old-hash", Now.AddMinutes(-2));
        _resets.Setup(r => r.FindLatestOfUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(old);

        await _handler.Handle(new ForgotPasswordCommand(Address), default);

        _dispatcher.Verify(d => d.EnqueueAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenALinkWasIssuedAMomentAgo_QueuesNothing()
    {
        User user = AccountIn(AccountStatus.Active);
        PasswordResetToken recent = PasswordResetToken.Issue(user.Id, "recent-hash", Now.AddSeconds(-30));
        _resets.Setup(r => r.FindLatestOfUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recent);

        Result result = await _handler.Handle(new ForgotPasswordCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownEmail_AnswersTheSameWayAndQueuesNothing()
    {
        Result result = await _handler.Handle(new ForgotPasswordCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _resets.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_ForAnAccountThatCannotSignIn_AnswersTheSameWayAndQueuesNothing(AccountStatus status)
    {
        AccountIn(status);

        Result result = await _handler.Handle(new ForgotPasswordCommand(Address), default);

        result.IsSuccess.Should().BeTrue();
        _resets.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-arroba")]
    public async Task Handle_WithAnInvalidEmail_FailsWithoutLookingForTheAccount(string address)
    {
        Result result = await _handler.Handle(new ForgotPasswordCommand(address), default);

        result.Error.Should().Be(Email.Invalid);
        _accounts.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    private User AccountIn(AccountStatus status)
    {
        User user = UserBuilder.InStatus(status, UserRole.Manager, Address);
        _accounts.Setup(a => a.FindByEmailAsync(It.Is<Email>(e => e.Value == Address), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}