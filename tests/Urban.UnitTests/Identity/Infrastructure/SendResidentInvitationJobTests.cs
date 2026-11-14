using System.Reflection;
using FluentAssertions;
using Hangfire;
using MediatR;
using Moq;
using Urban.Identity.Application.Invitations.SendResidentInvitation;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Identity.Infrastructure;

public class SendResidentInvitationJobTests
{
    private readonly Mock<ISender> _sender = new();

    [Fact]
    public async Task RunAsync_SendsTheCommandForTheGivenUser()
    {
        Guid userId = Guid.CreateVersion7();
        using CancellationTokenSource source = new();
        _sender.Setup(s => s.Send(It.IsAny<SendResidentInvitationCommand>(), source.Token)).ReturnsAsync(Result.Success());

        await new SendResidentInvitationJob(_sender.Object).RunAsync(userId, source.Token);

        _sender.Verify(s => s.Send(It.Is<SendResidentInvitationCommand>(c => c.UserId == userId), source.Token), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LetsFailuresReachHangfireSoTheRetryPolicyApplies()
    {
        _sender
            .Setup(s => s.Send(It.IsAny<SendResidentInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> run = () => new SendResidentInvitationJob(_sender.Object).RunAsync(Guid.CreateVersion7(), default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
    }

    [Fact]
    public void RunAsync_RetriesFiveTimesWithGrowingDelaysAndThenFails()
    {
        AutomaticRetryAttribute retry = RunMethod().GetCustomAttribute<AutomaticRetryAttribute>()!;

        retry.Attempts.Should().Be(5);
        retry.DelaysInSeconds.Should().Equal(60, 300, 900, 3600, 10800);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
        retry.DelaysInSeconds[0].Should().BeGreaterThanOrEqualTo((int)EmailLinkToken.ResendInterval.TotalSeconds);
    }

    [Fact]
    public void RunAsync_NeverRunsTwoAtOnce_SoTheOneEmailPerMinuteRuleHolds()
    {
        RunMethod().GetCustomAttribute<DisableConcurrentExecutionAttribute>().Should().NotBeNull();
    }

    private static MethodInfo RunMethod() => typeof(SendResidentInvitationJob).GetMethod(nameof(SendResidentInvitationJob.RunAsync))!;
}
