using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Administration;
using Urban.Identity.Application.Administration.GetPlatformSummary;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class GetPlatformSummaryQueryHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IManagerAccounts> _managers = new();
    private readonly GetPlatformSummaryQueryHandler _handler;

    public GetPlatformSummaryQueryHandlerTests()
    {
        _handler = new GetPlatformSummaryQueryHandler(_accounts.Object, _managers.Object);
    }

    [Fact]
    public async Task Handle_ForAnActiveSuperAdmin_ReturnsTheNumbersOfThePlatform()
    {
        User admin = Actor(UserRole.SuperAdmin, AccountStatus.Active);
        PlatformSummary summary = new(ActiveManagers: 12, SuspendedManagers: 2, AwaitingConfirmationManagers: 3, PendingRequests: 4, Condominiums: 11);
        _managers.Setup(m => m.SummarizeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(summary);

        Result<PlatformSummary> result = await _handler.Handle(new GetPlatformSummaryQuery(admin.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(summary);
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveSuperAdmin_IsForbiddenAndCountsNothing(UserRole role, AccountStatus status)
    {
        User actor = Actor(role, status);

        Result<PlatformSummary> result = await _handler.Handle(new GetPlatformSummaryQuery(actor.Id), Ct);

        result.Error.Should().Be(AdministrationErrors.Forbidden);
        _managers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownActor_IsForbidden()
    {
        Result<PlatformSummary> result = await _handler.Handle(new GetPlatformSummaryQuery(Guid.CreateVersion7()), Ct);

        result.Error.Should().Be(AdministrationErrors.Forbidden);
        _managers.VerifyNoOtherCalls();
    }

    private User Actor(UserRole role, AccountStatus status)
    {
        User user = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
