using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Enrollment;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ManagerEnrollmentTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly IManagerEnrollment _enrollment;

    public ManagerEnrollmentTests()
    {
        _enrollment = new ManagerEnrollment(_accounts.Object);
    }

    [Fact]
    public async Task Enroll_ForAManagerWithoutCondominium_LinksItAndSavesTheUser()
    {
        User manager = UserBuilder.ManagerWithoutCondominium();
        GivenUser(manager);

        Result result = await _enrollment.EnrollAsync(manager.Id, CondominiumId, "  Ana  Paula ", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        manager.CondominiumId.Should().Be(CondominiumId);
        manager.DisplayName!.Value.Should().Be("Ana Paula");
        _accounts.Verify(a => a.SaveEnrollmentAsync(manager, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Enroll_WithAnInvalidDisplayName_FailsWithoutReadingTheAccount()
    {
        Result result = await _enrollment.EnrollAsync(Guid.CreateVersion7(), CondominiumId, "A", CancellationToken.None);

        result.Error.Should().Be(DisplayName.Invalid);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Enroll_ForAnUnknownUser_FailsAsUnauthenticated()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _enrollment.EnrollAsync(userId, CondominiumId, "Ana", CancellationToken.None);

        result.Error.Should().Be(SessionErrors.Unauthenticated);
        NothingWasSaved();
    }

    [Theory]
    [InlineData(AccountStatus.Deactivated, "identity.user.account_deactivated")]
    [InlineData(AccountStatus.Suspended, "identity.user.account_suspended")]
    [InlineData(AccountStatus.Deleted, "identity.user.account_deleted")]
    public async Task Enroll_ForAnAccountThatCannotSignIn_FailsWithTheStateOfTheAccount(AccountStatus status, string code)
    {
        User manager = UserBuilder.ManagerWithoutCondominium(status);
        GivenUser(manager);

        Result result = await _enrollment.EnrollAsync(manager.Id, CondominiumId, "Ana", CancellationToken.None);

        result.Error.Code.Should().Be(code);
        manager.CondominiumId.Should().BeNull();
        NothingWasSaved();
    }

    [Fact]
    public async Task Enroll_ForAManagerWhoAlreadyHasACondominium_FailsAndSavesNothing()
    {
        User manager = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager);
        Guid current = manager.CondominiumId!.Value;
        GivenUser(manager);

        Result result = await _enrollment.EnrollAsync(manager.Id, CondominiumId, "Ana", CancellationToken.None);

        result.Error.Should().Be(UserErrors.CondominiumAlreadyAssigned);
        manager.CondominiumId.Should().Be(current);
        NothingWasSaved();
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Enroll_ForAnyRoleButManager_FailsAsNotManager(UserRole role)
    {
        User user = UserBuilder.InStatus(AccountStatus.Active, role);
        GivenUser(user);

        Result result = await _enrollment.EnrollAsync(user.Id, CondominiumId, "Ana", CancellationToken.None);

        result.Error.Should().Be(UserErrors.NotManager);
        NothingWasSaved();
    }

    private void GivenUser(User user) =>
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    private void NothingWasSaved() =>
        _accounts.Verify(a => a.SaveEnrollmentAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
}
