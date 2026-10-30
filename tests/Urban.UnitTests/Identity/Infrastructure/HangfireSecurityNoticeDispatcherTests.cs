using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class HangfireSecurityNoticeDispatcherTests
{
    private readonly Mock<IBackgroundJobClient> _client = new();
    private readonly HangfireSecurityNoticeDispatcher _dispatcher;
    private Job? _job;
    private IState? _state;

    public HangfireSecurityNoticeDispatcherTests()
    {
        _client
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((j, s) =>
            {
                _job = j;
                _state = s;
            })
            .Returns("1");
        _dispatcher = new HangfireSecurityNoticeDispatcher(_client.Object);
    }

    [Fact]
    public async Task EnqueueAsync_QueuesTheNoticeJobCarryingTheKindAndTheAddresses()
    {
        await _dispatcher.EnqueueAsync(SecurityNotice.EmailChangeRequested, Email.Create("a@example.com").Value, Email.Create("b@example.com").Value, default);

        _job!.Type.Should().Be<SendSecurityNoticeJob>();
        _job.Method.Name.Should().Be(nameof(SendSecurityNoticeJob.RunAsync));
        _job.Args[0].Should().Be(SecurityNotice.EmailChangeRequested);
        _job.Args[1].Should().Be("a@example.com");
        _job.Args[2].Should().Be("b@example.com");
        _job.Args.Should().HaveCount(4);
        _state.Should().BeOfType<EnqueuedState>();
    }

    [Fact]
    public async Task EnqueueAsync_WithoutASecondAddress_CarriesNull()
    {
        await _dispatcher.EnqueueAsync(SecurityNotice.PasswordChanged, Email.Create("a@example.com").Value, null, default);

        _job!.Args[2].Should().BeNull();
    }
}
