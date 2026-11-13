using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Application.RegistrationRequests.NotifyRegistrationRequest;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class NotifyRegistrationRequestCommandHandlerTests
{
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IRegistrationRequestNotifier> _notifier = new();
    private readonly NotifyRegistrationRequestCommandHandler _handler;

    public NotifyRegistrationRequestCommandHandlerTests()
    {
        _handler = new NotifyRegistrationRequestCommandHandler(_accounts.Object, _notifier.Object);
    }

    [Fact]
    public async Task Handle_ForAPendingRequest_SendsTheRequestDataToTheNotifier()
    {
        User request = UserBuilder.Requested(address: "ana@example.com");
        _accounts.Setup(a => a.FindByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        RegistrationRequestSummary? sent = null;
        _notifier
            .Setup(n => n.NotifyAsync(It.IsAny<RegistrationRequestSummary>(), It.IsAny<CancellationToken>()))
            .Callback<RegistrationRequestSummary, CancellationToken>((s, _) => sent = s)
            .Returns(Task.CompletedTask);

        Result result = await _handler.Handle(new NotifyRegistrationRequestCommand(request.Id), default);

        result.IsSuccess.Should().BeTrue();
        sent.Should().Be(new RegistrationRequestSummary(request.Id, "ana@example.com", "Ana Paula", "Residencial Vila Verde", UserBuilder.Now));
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_SucceedsWithoutNotifying()
    {
        Result result = await _handler.Handle(new NotifyRegistrationRequestCommand(Guid.CreateVersion7()), default);

        result.IsSuccess.Should().BeTrue();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnApprovedRequest_SucceedsWithoutNotifying()
    {
        User user = UserBuilder.Requested();
        user.Approve(UserBuilder.ActorId, UserBuilder.Now);

        await AssertNothingIsSentAsync(user);
    }

    [Fact]
    public async Task Handle_ForARejectedRequest_SucceedsWithoutNotifying()
    {
        await AssertNothingIsSentAsync(UserBuilder.Requested(AccountStatus.Rejected));
    }

    [Fact]
    public async Task Handle_ForAnActiveAccount_SucceedsWithoutNotifying()
    {
        await AssertNothingIsSentAsync(UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager));
    }

    [Fact]
    public async Task Handle_WhenTheNotifierFails_LetsTheFailureReachTheRetryPolicy()
    {
        User request = UserBuilder.Requested();
        _accounts.Setup(a => a.FindByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _notifier
            .Setup(n => n.NotifyAsync(It.IsAny<RegistrationRequestSummary>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falhou"));

        Func<Task> handle = () => _handler.Handle(new NotifyRegistrationRequestCommand(request.Id), default);

        await handle.Should().ThrowAsync<InvalidOperationException>();
    }

    private async Task AssertNothingIsSentAsync(User user)
    {
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        Result result = await _handler.Handle(new NotifyRegistrationRequestCommand(user.Id), default);

        result.IsSuccess.Should().BeTrue();
        _notifier.VerifyNoOtherCalls();
    }
}
