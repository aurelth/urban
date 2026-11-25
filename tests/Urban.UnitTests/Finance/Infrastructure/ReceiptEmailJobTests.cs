using System.Reflection;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MediatR;
using Moq;
using Urban.Finance.Application.Receipts;
using Urban.Finance.Infrastructure.Receipts;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Finance.Infrastructure;

public class ReceiptEmailJobTests
{
    [Fact]
    public async Task Enqueue_QueuesTheJobCarryingOnlyTheCondominiumAndThePayment()
    {
        Mock<IBackgroundJobClient> client = new();
        Guid condominiumId = Guid.CreateVersion7();
        Guid paymentId = Guid.CreateVersion7();
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

        await new HangfireReceiptEmailDispatcher(client.Object).EnqueueAsync(condominiumId, paymentId, default);

        job.Should().NotBeNull("o envio do recibo precisa ser enfileirado");
        job!.Type.Should().Be<SendReceiptEmailJob>();
        job.Method.Name.Should().Be(nameof(SendReceiptEmailJob.RunAsync));
        job.Args.Take(2).Should().Equal(condominiumId, paymentId);
        job.Args.Should().HaveCount(3);
        state.Should().BeOfType<EnqueuedState>();
    }

    [Fact]
    public async Task RunAsync_ChoosesTheCondominiumBeforeAnythingIsRead_ThenSendsTheCommand()
    {
        Mock<ITenantScope> tenant = new(MockBehavior.Strict);
        Mock<ISender> sender = new(MockBehavior.Strict);
        Guid condominiumId = Guid.CreateVersion7();
        Guid paymentId = Guid.CreateVersion7();
        MockSequence sequence = new();
        tenant.InSequence(sequence).Setup(scope => scope.Enter(condominiumId));
        sender.InSequence(sequence).Setup(s => s.Send(It.Is<SendReceiptEmailCommand>(c => c.PaymentId == paymentId), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        await new SendReceiptEmailJob(tenant.Object, sender.Object).RunAsync(condominiumId, paymentId, default);

        tenant.Verify(scope => scope.Enter(condominiumId), Times.Once);
        sender.Verify(s => s.Send(It.IsAny<SendReceiptEmailCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LetsTheProviderFailureReachHangfireSoTheRetryPolicyApplies()
    {
        Mock<ISender> sender = new();
        sender
            .Setup(s => s.Send(It.IsAny<SendReceiptEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> run = () => new SendReceiptEmailJob(Mock.Of<ITenantScope>(), sender.Object).RunAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
    }

    [Fact]
    public void RunAsync_RetriesFiveTimesWithGrowingDelaysAndThenFails()
    {
        AutomaticRetryAttribute? retry = typeof(SendReceiptEmailJob).GetMethod(nameof(SendReceiptEmailJob.RunAsync))!.GetCustomAttribute<AutomaticRetryAttribute>();

        retry.Should().NotBeNull();
        retry!.Attempts.Should().Be(5);
        retry.DelaysInSeconds.Should().Equal(60, 300, 900, 3600, 10800);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }
}
