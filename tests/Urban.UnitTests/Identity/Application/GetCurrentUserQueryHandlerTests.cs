using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Me;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<ICondominiumDirectory> _directory = new();
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _handler = new GetCurrentUserQueryHandler(_accounts.Object, _directory.Object);
    }

    [Fact]
    public async Task Handle_ForAnActiveUser_ReturnsWhoTheyAre()
    {
        User user = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager, "sindica@example.com");
        GivenUser(user);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new CurrentUser(user.Id, user.Email.Value, "Manager", user.CondominiumId, null));
        user.CondominiumId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ForAnEnrolledManager_ReturnsHowTheyPreferToBeCalled()
    {
        User manager = UserBuilder.EnrolledManager("Ana Paula");
        GivenUser(manager);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(manager.Id), CancellationToken.None);

        result.Value.DisplayName.Should().Be("Ana Paula");
        result.Value.CondominiumId.Should().Be(manager.CondominiumId);
    }

    [Fact]
    public async Task Handle_ForAManagerWithoutCondominium_HasNoCondominiumAndNoDisplayName()
    {
        User manager = UserBuilder.ManagerWithoutCondominium();
        GivenUser(manager);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(manager.Id), CancellationToken.None);

        result.Value.CondominiumId.Should().BeNull();
        result.Value.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForASuperAdmin_HasNoCondominium()
    {
        User user = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin);
        GivenUser(user);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        result.Value.Role.Should().Be("SuperAdmin");
        result.Value.CondominiumId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_FailsAsUnauthenticated()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(userId), CancellationToken.None);

        result.Error.Should().Be(SessionErrors.Unauthenticated);
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification, "identity.user.email_not_verified")]
    [InlineData(AccountStatus.Deactivated, "identity.user.account_deactivated")]
    [InlineData(AccountStatus.Suspended, "identity.user.account_suspended")]
    [InlineData(AccountStatus.Deleted, "identity.user.account_deleted")]
    public async Task Handle_ForAnAccountThatCannotSignIn_FailsWithTheStateOfTheAccount(AccountStatus status, string code)
    {
        User user = UserBuilder.InStatus(status);
        GivenUser(user);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        result.Error.Code.Should().Be(code);
    }

    private void GivenUser(User user) =>
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    [Fact]
    public async Task Handle_ForAResidentWhoNeverChoseAName_UsesTheFirstNameOfTheirRegistration()
    {
        User resident = UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, "maria@example.com");
        GivenUser(resident);
        _directory.Setup(d => d.FindFirstNameAsync(resident.Id, It.IsAny<CancellationToken>())).ReturnsAsync("Maria");

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(resident.Id), CancellationToken.None);

        result.Value.DisplayName.Should().Be("Maria");
        result.Value.Email.Should().Be("maria@example.com");
    }

    [Fact]
    public async Task Handle_ForAResidentWithoutARegistration_HasNoDisplayName()
    {
        User resident = UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident);
        GivenUser(resident);
        _directory.Setup(d => d.FindFirstNameAsync(resident.Id, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(resident.Id), CancellationToken.None);

        result.Value.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForAnAccountThatChoseAName_KeepsItAndDoesNotAskTheRegistration()
    {
        User manager = UserBuilder.EnrolledManager("Ana Paula");
        GivenUser(manager);

        Result<CurrentUser> result = await _handler.Handle(new GetCurrentUserQuery(manager.Id), CancellationToken.None);

        result.Value.DisplayName.Should().Be("Ana Paula");
        _directory.Verify(d => d.FindFirstNameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAnAccountWithoutCondominium_DoesNotAskTheRegistration()
    {
        User superAdmin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin);
        User manager = UserBuilder.ManagerWithoutCondominium();
        GivenUser(superAdmin);
        GivenUser(manager);

        await _handler.Handle(new GetCurrentUserQuery(superAdmin.Id), CancellationToken.None);
        await _handler.Handle(new GetCurrentUserQuery(manager.Id), CancellationToken.None);

        _directory.Verify(d => d.FindFirstNameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
