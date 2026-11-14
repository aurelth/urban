using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class HangfireResidentInvitationDispatcherTests
{
    [Fact]
    public async Task EnqueueAsync_QueuesTheInvitationJobCarryingOnlyTheUserId()
    {
        Mock<IBackgroundJobClient> client = new();
        Guid userId = Guid.CreateVersion7();
        Job? job = null;
        IState? state = null;
        client
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((j, s) =>
            {
                job = j;
                state = s;
            })
            .Returns("1");

        await new HangfireResidentInvitationDispatcher(client.Object).EnqueueAsync(userId, default);

        job!.Type.Should().Be<SendResidentInvitationJob>();
        job.Method.Name.Should().Be(nameof(SendResidentInvitationJob.RunAsync));
        job.Args[0].Should().Be(userId);
        job.Args.Should().HaveCount(2);
        state.Should().BeOfType<EnqueuedState>();
    }
}
