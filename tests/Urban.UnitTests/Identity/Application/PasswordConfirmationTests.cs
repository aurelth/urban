using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.Enrollment;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class PasswordConfirmationTests
{
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly IPasswordConfirmation _confirmation;

    public PasswordConfirmationTests()
    {
        _confirmation = new PasswordConfirmation(_accounts.Object);
    }

    [Fact]
    public async Task Confirm_WithTheRightPassword_Succeeds_CheckingItAgainstTheAddressOfTheAccount()
    {
        User manager = Given(UserBuilder.EnrolledManager("Ana", "sindica@example.com"));
        _accounts
            .Setup(a => a.CheckCredentialsAsync(manager.Email, "Senha-certa-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(manager));

        Result result = await _confirmation.ConfirmAsync(manager.Id, "Senha-certa-1", default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WithAWrongPassword_FailsAsPasswordIncorrect_ItIsNotALoginError()
    {
        User manager = Given(UserBuilder.EnrolledManager("Ana"));
        _accounts
            .Setup(a => a.CheckCredentialsAsync(manager.Email, "errada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<User>(LoginErrors.InvalidCredentials));

        Result result = await _confirmation.ConfirmAsync(manager.Id, "errada", default);

        result.Error.Should().Be(UserErrors.PasswordIncorrect);
    }

    [Fact]
    public async Task Confirm_ForALockedAccount_KeepsTheLockoutError()
    {
        User manager = Given(UserBuilder.EnrolledManager("Ana"));
        _accounts
            .Setup(a => a.CheckCredentialsAsync(manager.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<User>(LoginErrors.LockedOut));

        Result result = await _confirmation.ConfirmAsync(manager.Id, "qualquer", default);

        result.Error.Should().Be(LoginErrors.LockedOut);
    }

    [Fact]
    public async Task Confirm_WithoutAPassword_IsTreatedAsAWrongOne()
    {
        User manager = Given(UserBuilder.EnrolledManager("Ana"));
        _accounts
            .Setup(a => a.CheckCredentialsAsync(manager.Email, string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<User>(LoginErrors.InvalidCredentials));

        Result result = await _confirmation.ConfirmAsync(manager.Id, null, default);

        result.Error.Should().Be(UserErrors.PasswordIncorrect);
    }

    [Fact]
    public async Task Confirm_ForAnUnknownUser_FailsAsUnauthenticated()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _confirmation.ConfirmAsync(userId, "Senha-certa-1", default);

        result.Error.Should().Be(SessionErrors.Unauthenticated);
    }

    [Theory]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.PendingVerification)]
    public async Task Confirm_ForAnAccountThatCannotSignIn_FailsWithoutCheckingThePassword(AccountStatus status)
    {
        User user = Given(UserBuilder.InStatus(status, UserRole.Manager));

        Result result = await _confirmation.ConfirmAsync(user.Id, "Senha-certa-1", default);

        result.IsFailure.Should().BeTrue();
        _accounts.Verify(a => a.CheckCredentialsAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private User Given(User user)
    {
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
