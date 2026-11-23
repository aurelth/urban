using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Accounts;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class AccountEmailsTests
{
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly IAccountEmails _emails;

    public AccountEmailsTests()
    {
        _emails = new AccountEmails(_accounts.Object);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.Manager)]
    public async Task ReturnsTheLoginEmailOfAnActiveAccountOfThatCondominium(UserRole role)
    {
        Guid condominiumId = Guid.CreateVersion7();
        User user = Given(UserBuilder.InStatus(AccountStatus.Active, role, "ana@example.com", condominiumId));

        string? email = await _emails.FindActiveEmailAsync(user.Id, condominiumId, default);

        email.Should().Be("ana@example.com");
    }

    [Fact]
    public async Task ForAnUnknownAccount_ReturnsNull()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        (await _emails.FindActiveEmailAsync(userId, Guid.CreateVersion7(), default)).Should().BeNull();
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task ForAnAccountThatCannotEnter_ReturnsNull(AccountStatus status)
    {
        Guid condominiumId = Guid.CreateVersion7();
        User user = Given(UserBuilder.InStatus(status, UserRole.Resident, "ana@example.com", condominiumId));

        (await _emails.FindActiveEmailAsync(user.Id, condominiumId, default)).Should().BeNull();
    }

    [Fact]
    public async Task ForAnAccountOfAnotherCondominium_ReturnsNull()
    {
        User user = Given(UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, "ana@example.com", Guid.CreateVersion7()));

        (await _emails.FindActiveEmailAsync(user.Id, Guid.CreateVersion7(), default)).Should().BeNull();
    }

    [Fact]
    public async Task ForTheSuperAdmin_ReturnsNull_BecauseTheMuralIsNotTheirs()
    {
        User user = Given(UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com"));

        (await _emails.FindActiveEmailAsync(user.Id, Guid.CreateVersion7(), default)).Should().BeNull();
    }

    private User Given(User user)
    {
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        return user;
    }
}
