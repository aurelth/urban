using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class HangfireRegistrationRequestDispatcherTests
{
    private readonly Mock<IBackgroundJobClient> _client = new();
    private readonly HangfireRegistrationRequestDispatcher _dispatcher;

    public HangfireRegistrationRequestDispatcherTests()
    {
        _dispatcher = new HangfireRegistrationRequestDispatcher(_client.Object);
    }

    [Fact]
    public async Task EnqueueAsync_QueuesTheNotifyJobCarryingOnlyTheUserId()
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

        job!.Type.Should().Be<NotifyRegistrationRequestJob>();
        job.Method.Name.Should().Be(nameof(NotifyRegistrationRequestJob.RunAsync));
        job.Args[0].Should().Be(userId);
        job.Args.Should().HaveCount(2);
        state.Should().BeOfType<EnqueuedState>();
    }
}
