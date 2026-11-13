using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Application.RegistrationRequests.ListRegistrationRequests;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ListRegistrationRequestsQueryHandlerTests
{
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IRegistrationRequests> _requests = new();
    private readonly ListRegistrationRequestsQueryHandler _handler;

    public ListRegistrationRequestsQueryHandlerTests()
    {
        _handler = new ListRegistrationRequestsQueryHandler(_accounts.Object, _requests.Object);
    }

    [Fact]
    public async Task Handle_ForAnActiveSuperAdmin_ReturnsThePendingRequests()
    {
        User admin = Actor(UserRole.SuperAdmin, AccountStatus.Active);
        RegistrationRequestSummary[] pending =
        [
            new(Guid.CreateVersion7(), "a@example.com", "Ana", "Vila Verde", UserBuilder.Now),
            new(Guid.CreateVersion7(), "b@example.com", "Bia", "Parque Sol", UserBuilder.Now.AddMinutes(5)),
        ];
        _requests.Setup(r => r.ListPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(pending);

        Result<IReadOnlyList<RegistrationRequestSummary>> result = await _handler.Handle(new ListRegistrationRequestsQuery(admin.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(pending);
    }

    [Fact]
    public async Task Handle_WithNoPendingRequests_ReturnsAnEmptyList()
    {
        User admin = Actor(UserRole.SuperAdmin, AccountStatus.Active);
        _requests.Setup(r => r.ListPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        Result<IReadOnlyList<RegistrationRequestSummary>> result = await _handler.Handle(new ListRegistrationRequestsQuery(admin.Id), default);

        result.Value.Should().BeEmpty();
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Deactivated)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveSuperAdmin_IsForbiddenAndReadsNothing(UserRole role, AccountStatus status)
    {
        User actor = Actor(role, status);

        Result<IReadOnlyList<RegistrationRequestSummary>> result = await _handler.Handle(new ListRegistrationRequestsQuery(actor.Id), default);

        result.Error.Should().Be(RegistrationRequestErrors.Forbidden);
        _requests.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownActor_IsForbidden()
    {
        Result<IReadOnlyList<RegistrationRequestSummary>> result = await _handler.Handle(new ListRegistrationRequestsQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(RegistrationRequestErrors.Forbidden);
        _requests.VerifyNoOtherCalls();
    }

    private User Actor(UserRole role, AccountStatus status)
    {
        User user = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
