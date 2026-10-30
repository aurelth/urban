using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class HangfireEmailChangeDispatcherTests
{
    private readonly Mock<IBackgroundJobClient> _client = new();
    private readonly HangfireEmailChangeDispatcher _dispatcher;

    public HangfireEmailChangeDispatcherTests()
    {
        _dispatcher = new HangfireEmailChangeDispatcher(_client.Object);
    }

    [Fact]
    public async Task EnqueueAsync_QueuesTheSendJobCarryingTheUserIdAndTheNewAddress_AndNoSecret()
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

        await _dispatcher.EnqueueAsync(userId, Email.Create("nova@example.com").Value, default);

        job!.Type.Should().Be<SendEmailChangeJob>();
        job.Method.Name.Should().Be(nameof(SendEmailChangeJob.RunAsync));
        job.Args[0].Should().Be(userId);
        job.Args[1].Should().Be("nova@example.com");
        job.Args.Should().HaveCount(3);
        state.Should().BeOfType<EnqueuedState>();
    }
}
