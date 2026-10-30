using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.EmailChange.RequestEmailChange;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class RequestEmailChangeCommandHandlerTests
{
    private const string Password = "Senha-atual-123";
    private const string NewAddress = "nova@example.com";

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailChangeDispatcher> _links = new();
    private readonly Mock<ISecurityNoticeDispatcher> _notices = new();
    private readonly User _user = UserBuilder.EnrolledManager("Ana", "sindica@example.com");
    private readonly RequestEmailChangeCommandHandler _handler;

    public RequestEmailChangeCommandHandlerTests()
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _accounts
            .Setup(a => a.CheckCredentialsAsync(_user.Email, Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_user));
        _handler = new RequestEmailChangeCommandHandler(_accounts.Object, _links.Object, _notices.Object);
    }

    [Fact]
    public async Task Handle_WithTheRightPasswordAndAFreeAddress_QueuesTheLinkToTheNewAddress_AndTheNoticeToTheCurrentOne()
    {
        Result result = await _handler.Handle(new RequestEmailChangeCommand(_user.Id, Password, " Nova@Example.com "), default);

        result.IsSuccess.Should().BeTrue();
        Email requested = Email.Create(NewAddress).Value;
        _links.Verify(l => l.EnqueueAsync(_user.Id, requested, It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.EnqueueAsync(SecurityNotice.EmailChangeRequested, _user.Email, requested, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAWrongPassword_FailsAsPasswordIncorrect_AndSendsNothing()
    {
        _accounts
            .Setup(a => a.CheckCredentialsAsync(_user.Email, "errada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<User>(LoginErrors.InvalidCredentials));

        Result result = await _handler.Handle(new RequestEmailChangeCommand(_user.Id, "errada", NewAddress), default);

        result.Error.Should().Be(UserErrors.PasswordIncorrect);
        NothingWasSent();
    }

    [Fact]
    public async Task Handle_ForALockedAccount_KeepsTheLockoutError()
    {
        _accounts
            .Setup(a => a.CheckCredentialsAsync(_user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<User>(LoginErrors.LockedOut));

        Result result = await _handler.Handle(new RequestEmailChangeCommand(_user.Id, Password, NewAddress), default);

        result.Error.Should().Be(LoginErrors.LockedOut);
        NothingWasSent();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData(null)]
    public async Task Handle_WithAnInvalidAddress_FailsWithTheEmailError_WithoutSpendingAnAttempt(string? address)
    {
        Result result = await _handler.Handle(new RequestEmailChangeCommand(_user.Id, Password, address), default);

        result.Error.Should().Be(Email.Invalid);
        _accounts.Verify(a => a.CheckCredentialsAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        NothingWasSent();
    }

    [Fact]
    public async Task Handle_WithTheCurrentAddress_FailsAsUnchanged_WithoutSpendingAnAttempt()
    {
        Result result = await _handler.Handle(new RequestEmailChangeCommand(_user.Id, Password, "SINDICA@example.com"), default);

        result.Error.Should().Be(UserErrors.EmailUnchanged);
        _accounts.Verify(a => a.CheckCredentialsAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        NothingWasSent();
    }

    [Fact]
    public async Task Handle_WhenTheAddressBelongsToAnotherAccount_AnswersTheSameWay_ButSendsNothing()
    {
        User other = UserBuilder.InStatus(AccountStatus.Active, Urban.Identity.Domain.Access.UserRole.Resident, NewAddress);
        _accounts.Setup(a => a.FindByEmailAsync(other.Email, It.IsAny<CancellationToken>())).ReturnsAsync(other);

        Result result = await _handler.Handle(new RequestEmailChangeCommand(_user.Id, Password, NewAddress), default);

        result.IsSuccess.Should().BeTrue("a resposta não pode revelar quais e-mails já têm conta");
        NothingWasSent();
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_FailsAsUnauthenticated()
    {
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _handler.Handle(new RequestEmailChangeCommand(_user.Id, Password, NewAddress), default);

        result.Error.Should().Be(SessionErrors.Unauthenticated);
        NothingWasSent();
    }

    [Fact]
    public void TheCommand_NeverPrintsThePassword()
    {
        new RequestEmailChangeCommand(_user.Id, Password, NewAddress).ToString().Should().NotContain(Password);
    }

    private void NothingWasSent()
    {
        _links.VerifyNoOtherCalls();
        _notices.VerifyNoOtherCalls();
    }
}
