using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class HangfireContactMessageDispatcherTests
{
    private readonly Mock<IBackgroundJobClient> _client = new();
    private readonly HangfireContactMessageDispatcher _dispatcher;

    public HangfireContactMessageDispatcherTests()
    {
        _dispatcher = new HangfireContactMessageDispatcher(_client.Object);
    }

    [Fact]
    public async Task EnqueueAsync_QueuesTheNotifyJobCarryingOnlyTheMessageId()
    {
        Guid messageId = Guid.CreateVersion7();
        Job? job = null;
        IState? state = null;
        _client
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((j, s) =>
            {
                job = j;
                state = s;
            })
            .Returns("1");

        await _dispatcher.EnqueueAsync(messageId, default);

        job!.Type.Should().Be<NotifyContactMessageJob>();
        job.Method.Name.Should().Be(nameof(NotifyContactMessageJob.RunAsync));
        job.Args[0].Should().Be(messageId);
        job.Args.Should().HaveCount(2);
        state.Should().BeOfType<EnqueuedState>();
    }
}
