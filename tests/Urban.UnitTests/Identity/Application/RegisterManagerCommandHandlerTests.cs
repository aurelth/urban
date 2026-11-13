using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Registration.RegisterManager;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class RegisterManagerCommandHandlerTests
{
    private const string Address = "sindica@example.com";
    private const string FullName = "Ana Paula";
    private const string Condominium = "Residencial Vila Verde";

    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailVerifications> _verifications = new();
    private readonly Mock<IEmailVerificationDispatcher> _linkDispatcher = new();
    private readonly Mock<IRegistrationRequestDispatcher> _requestDispatcher = new();
    private readonly Mock<IClock> _clock = new();
    private readonly RegisterManagerCommandHandler _handler;

    public RegisterManagerCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new RegisterManagerCommandHandler(
            _accounts.Object,
            _verifications.Object,
            _linkDispatcher.Object,
            _requestDispatcher.Object,
            _clock.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData(null)]
    public async Task Handle_WithAnInvalidEmail_FailsWithoutTouchingAnything(string? address)
    {
        Result result = await _handler.Handle(Command(address!), default);

        result.Error.Should().Be(Email.Invalid);
        VerifyNothingHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Ana 123")]
    [InlineData(null)]
    public async Task Handle_WithAnInvalidName_FailsWithoutTouchingAnything(string? name)
    {
        Result result = await _handler.Handle(Command(Address, name!), default);

        result.Error.Should().Be(DisplayName.Invalid);
        VerifyNothingHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData(null)]
    public async Task Handle_WithAnInvalidCondominiumName_FailsWithoutTouchingAnything(string? condominium)
    {
        Result result = await _handler.Handle(Command(Address, FullName, condominium!), default);

        result.Error.Should().Be(RequestedCondominiumName.Invalid);
        VerifyNothingHappened();
    }

    [Fact]
    public async Task Handle_WithANewEmail_StoresARequestWaitingForApprovalAndNotifiesTheOwner()
    {
        User? stored = null;
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => stored = u)
            .ReturnsAsync(true);

        Result result = await _handler.Handle(Command(" Sindica@Example.com ", "  Ana   Paula ", " Residencial Vila Verde "), default);

        result.IsSuccess.Should().BeTrue();
        stored!.Email.Value.Should().Be(Address);
        stored.Role.Should().Be(UserRole.Manager);
        stored.Status.Should().Be(AccountStatus.PendingApproval);
        stored.CondominiumId.Should().BeNull();
        stored.DisplayName!.Value.Should().Be(FullName);
        stored.RequestedCondominiumName!.Value.Should().Be(Condominium);
        _requestDispatcher.Verify(d => d.EnqueueAsync(stored.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithANewEmail_SendsNothingToThePersonUntilTheOwnerApproves()
    {
        await _handler.Handle(Command(), default);

        _linkDispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenAnotherRegistrationWinsTheRace_SucceedsWithoutQueueingAnything()
    {
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(Command(), default);

        result.IsSuccess.Should().BeTrue();
        _linkDispatcher.VerifyNoOtherCalls();
        _requestDispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnApprovedAccountWithAnOldLink_QueuesANewLink()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        EmailVerificationToken old = EmailVerificationToken.Issue(pending.Id, "old-hash", Now.AddMinutes(-1));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(old);

        Result result = await _handler.Handle(Command(), default);

        result.IsSuccess.Should().BeTrue();
        _linkDispatcher.Verify(d => d.EnqueueAsync(pending.Id, It.IsAny<CancellationToken>()), Times.Once);
        _verifications.Verify(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _requestDispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnApprovedAccountWithoutAnyLink_QueuesANewLink()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync((EmailVerificationToken?)null);

        await _handler.Handle(Command(), default);

        _linkDispatcher.Verify(d => d.EnqueueAsync(pending.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForAnApprovedAccountWithARecentLink_SucceedsWithoutQueueingAgain()
    {
        User pending = ExistingUser(AccountStatus.PendingVerification);
        EmailVerificationToken recent = EmailVerificationToken.Issue(pending.Id, "recent-hash", Now.AddSeconds(-30));
        _verifications.Setup(v => v.FindLatestOfUserAsync(pending.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recent);

        Result result = await _handler.Handle(Command(), default);

        result.IsSuccess.Should().BeTrue();
        _linkDispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountStatus.PendingApproval)]
    [InlineData(AccountStatus.Rejected)]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_ForAnAccountThatIsNotWaitingForTheLink_SucceedsAndDoesNothing(AccountStatus status)
    {
        ExistingUser(status);

        Result result = await _handler.Handle(Command(), default);

        result.IsSuccess.Should().BeTrue();
        _verifications.VerifyNoOtherCalls();
        _linkDispatcher.VerifyNoOtherCalls();
        _requestDispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRole.SuperAdmin)]
    [InlineData(UserRole.Resident)]
    public async Task Handle_ForAPendingAccountThatIsNotAManager_SucceedsAndDoesNothing(UserRole role)
    {
        ExistingUser(AccountStatus.PendingVerification, role);

        Result result = await _handler.Handle(Command(), default);

        result.IsSuccess.Should().BeTrue();
        _verifications.VerifyNoOtherCalls();
        _linkDispatcher.VerifyNoOtherCalls();
        _requestDispatcher.VerifyNoOtherCalls();
    }

    private static RegisterManagerCommand Command(string email = Address, string name = FullName, string condominium = Condominium) =>
        new(email, name, condominium);

    private void VerifyNothingHappened()
    {
        _accounts.VerifyNoOtherCalls();
        _verifications.VerifyNoOtherCalls();
        _linkDispatcher.VerifyNoOtherCalls();
        _requestDispatcher.VerifyNoOtherCalls();
    }

    private User ExistingUser(AccountStatus status, UserRole role = UserRole.Manager)
    {
        User user = UserBuilder.InStatus(status, role, Address);
        _accounts.Setup(a => a.FindByEmailAsync(It.Is<Email>(e => e.Value == Address), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
