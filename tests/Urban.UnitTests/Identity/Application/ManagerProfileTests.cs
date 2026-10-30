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

public class ManagerProfileTests
{
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly IManagerProfile _profile;

    public ManagerProfileTests()
    {
        _profile = new ManagerProfile(_accounts.Object);
    }

    [Fact]
    public async Task GetDisplayName_ReturnsHowTheManagerPrefersToBeCalled()
    {
        User manager = Given(UserBuilder.EnrolledManager("Ana"));

        string? name = await _profile.GetDisplayNameAsync(manager.Id, default);

        name.Should().Be("Ana");
    }

    [Fact]
    public async Task GetDisplayName_ForAnUnknownUser_ReturnsNull()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        (await _profile.GetDisplayNameAsync(userId, default)).Should().BeNull();
    }

    [Fact]
    public async Task UpdateDisplayName_ChangesTheNameAndSavesTheUser_ReturningTheNormalizedName()
    {
        User manager = Given(UserBuilder.EnrolledManager("Ana"));

        Result<string> result = await _profile.UpdateDisplayNameAsync(manager.Id, "  Ana   Paula ", default);

        result.Value.Should().Be("Ana Paula");
        manager.DisplayName!.Value.Should().Be("Ana Paula");
        _accounts.Verify(a => a.SaveEnrollmentAsync(manager, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("A")]
    public async Task UpdateDisplayName_WithAnInvalidName_FailsWithoutReadingTheAccount(string? name)
    {
        Result<string> result = await _profile.UpdateDisplayNameAsync(Guid.CreateVersion7(), name, default);

        result.Error.Should().Be(DisplayName.Invalid);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateDisplayName_ForAnUnknownUser_FailsAsUnauthenticated()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result<string> result = await _profile.UpdateDisplayNameAsync(userId, "Ana", default);

        result.Error.Should().Be(SessionErrors.Unauthenticated);
    }

    [Fact]
    public async Task UpdateDisplayName_ForAnAccountThatCannotSignIn_FailsAndSavesNothing()
    {
        User suspended = Given(UserBuilder.InStatus(AccountStatus.Suspended, UserRole.Manager));

        Result<string> result = await _profile.UpdateDisplayNameAsync(suspended.Id, "Ana", default);

        result.Error.Should().Be(UserErrors.AccountSuspended);
        _accounts.Verify(a => a.SaveEnrollmentAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDisplayName_ForAManagerWithoutCondominium_FailsAsNotEnrolled()
    {
        User manager = Given(UserBuilder.ManagerWithoutCondominium());

        Result<string> result = await _profile.UpdateDisplayNameAsync(manager.Id, "Ana", default);

        result.Error.Should().Be(UserErrors.CondominiumRequired);
        _accounts.Verify(a => a.SaveEnrollmentAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private User Given(User user)
    {
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
