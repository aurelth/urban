using System.Reflection;
using FluentAssertions;
using Hangfire;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Identity.Infrastructure;

public class SendSecurityNoticeJobTests
{
    private readonly Mock<ISecurityNoticeNotifier> _notifier = new();
    private readonly SendSecurityNoticeJob _job;

    public SendSecurityNoticeJobTests()
    {
        _job = new SendSecurityNoticeJob(_notifier.Object);
    }

    [Fact]
    public async Task RunAsync_HandsTheNoticeAndTheAddressesToTheNotifier()
    {
        using CancellationTokenSource source = new();

        await _job.RunAsync(SecurityNotice.EmailChanged, "a@example.com", "b@example.com", source.Token);

        _notifier.Verify(
            n => n.SendAsync(
                SecurityNotice.EmailChanged,
                It.Is<Email>(e => e.Value == "a@example.com"),
                It.Is<Email?>(e => e != null && e.Value == "b@example.com"),
                source.Token),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithoutASecondAddress_PassesNull()
    {
        await _job.RunAsync(SecurityNotice.PasswordChanged, "a@example.com", null, default);

        _notifier.Verify(n => n.SendAsync(SecurityNotice.PasswordChanged, It.IsAny<Email>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LetsFailuresReachHangfireSoTheRetryPolicyApplies()
    {
        _notifier
            .Setup(n => n.SendAsync(It.IsAny<SecurityNotice>(), It.IsAny<Email>(), It.IsAny<Email?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> run = () => _job.RunAsync(SecurityNotice.PasswordChanged, "a@example.com", null, default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
    }

    [Fact]
    public void RunAsync_RetriesFiveTimesWithGrowingDelaysAndThenFails()
    {
        AutomaticRetryAttribute retry = typeof(SendSecurityNoticeJob).GetMethod(nameof(SendSecurityNoticeJob.RunAsync))!
            .GetCustomAttribute<AutomaticRetryAttribute>()!;

        retry.Attempts.Should().Be(5);
        retry.DelaysInSeconds.Should().Equal(60, 300, 900, 3600, 10800);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }
}
