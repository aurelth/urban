using System.Reflection;
using FluentAssertions;
using Hangfire;
using MediatR;
using Moq;
using Urban.Identity.Application.Contact.NotifyContactMessage;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Identity.Infrastructure;

public class NotifyContactMessageJobTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly NotifyContactMessageJob _job;

    public NotifyContactMessageJobTests()
    {
        _job = new NotifyContactMessageJob(_sender.Object);
    }

    [Fact]
    public async Task RunAsync_SendsTheCommandForTheGivenMessage()
    {
        Guid messageId = Guid.CreateVersion7();
        using CancellationTokenSource source = new();
        _sender
            .Setup(s => s.Send(It.IsAny<NotifyContactMessageCommand>(), source.Token))
            .ReturnsAsync(Result.Success());

        await _job.RunAsync(messageId, source.Token);

        _sender.Verify(s => s.Send(It.Is<NotifyContactMessageCommand>(c => c.MessageId == messageId), source.Token), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LetsFailuresReachHangfireSoTheRetryPolicyApplies()
    {
        _sender
            .Setup(s => s.Send(It.IsAny<NotifyContactMessageCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> run = () => _job.RunAsync(Guid.CreateVersion7(), default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
    }

    [Fact]
    public void RunAsync_RetriesFiveTimesWithGrowingDelaysAndThenFails()
    {
        AutomaticRetryAttribute retry = typeof(NotifyContactMessageJob)
            .GetMethod(nameof(NotifyContactMessageJob.RunAsync))!
            .GetCustomAttribute<AutomaticRetryAttribute>()!;

        retry.Attempts.Should().Be(5);
        retry.DelaysInSeconds.Should().Equal(60, 300, 900, 3600, 10800);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }
}
