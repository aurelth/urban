using System.Reflection;
using FluentAssertions;
using Hangfire;
using MediatR;
using Moq;
using Urban.Identity.Application.EmailChange.SendEmailChange;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Identity.Infrastructure;

public class SendEmailChangeJobTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly SendEmailChangeJob _job;

    public SendEmailChangeJobTests()
    {
        _job = new SendEmailChangeJob(_sender.Object);
    }

    [Fact]
    public async Task RunAsync_SendsTheCommandForTheGivenUserAndAddress()
    {
        Guid userId = Guid.CreateVersion7();
        using CancellationTokenSource source = new();
        _sender
            .Setup(s => s.Send(It.IsAny<SendEmailChangeCommand>(), source.Token))
            .ReturnsAsync(Result.Success());

        await _job.RunAsync(userId, "nova@example.com", source.Token);

        _sender.Verify(
            s => s.Send(It.Is<SendEmailChangeCommand>(c => c.UserId == userId && c.NewEmail == "nova@example.com"), source.Token),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_LetsFailuresReachHangfireSoTheRetryPolicyApplies()
    {
        _sender
            .Setup(s => s.Send(It.IsAny<SendEmailChangeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> run = () => _job.RunAsync(Guid.CreateVersion7(), "nova@example.com", default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
    }

    [Fact]
    public void RunAsync_RetriesFiveTimesWithGrowingDelaysAndThenFails()
    {
        AutomaticRetryAttribute retry = RunMethod().GetCustomAttribute<AutomaticRetryAttribute>()!;

        retry.Attempts.Should().Be(5);
        retry.DelaysInSeconds.Should().Equal(60, 300, 900, 3600, 10800);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }

    [Fact]
    public void RunAsync_WaitsAtLeastTheResendIntervalBeforeTheFirstRetry()
    {
        AutomaticRetryAttribute retry = RunMethod().GetCustomAttribute<AutomaticRetryAttribute>()!;

        retry.DelaysInSeconds[0].Should().BeGreaterThanOrEqualTo((int)EmailChangeToken.ResendInterval.TotalSeconds);
    }

    [Fact]
    public void RunAsync_NeverRunsTwoAtOnce()
    {
        RunMethod().GetCustomAttribute<DisableConcurrentExecutionAttribute>().Should().NotBeNull();
    }

    private static MethodInfo RunMethod() => typeof(SendEmailChangeJob).GetMethod(nameof(SendEmailChangeJob.RunAsync))!;
}
