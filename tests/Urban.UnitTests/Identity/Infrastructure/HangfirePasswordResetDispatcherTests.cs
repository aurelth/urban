using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class HangfirePasswordResetDispatcherTests
{
    private readonly Mock<IBackgroundJobClient> _client = new();
    private readonly HangfirePasswordResetDispatcher _dispatcher;

    public HangfirePasswordResetDispatcherTests()
    {
        _dispatcher = new HangfirePasswordResetDispatcher(_client.Object);
    }

    [Fact]
    public async Task EnqueueAsync_QueuesTheSendJobCarryingOnlyTheUserId()
    {
        Guid userId = Guid.CreateVersion7();
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

        await _dispatcher.EnqueueAsync(userId, default);

        job!.Type.Should().Be<SendPasswordResetJob>();
        job.Method.Name.Should().Be(nameof(SendPasswordResetJob.RunAsync));
        job.Args[0].Should().Be(userId);
        job.Args.Should().HaveCount(2);
        state.Should().BeOfType<EnqueuedState>();
    }
}