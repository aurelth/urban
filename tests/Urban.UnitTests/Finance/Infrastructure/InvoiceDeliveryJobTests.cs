using System.Reflection;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MediatR;
using Moq;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Infrastructure.Invoices;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Finance.Infrastructure;

public class InvoiceDeliveryJobTests
{
    [Fact]
    public async Task Enqueue_QueuesTheJobCarryingOnlyTheCondominiumAndTheCharge()
    {
        Mock<IBackgroundJobClient> client = new();
        Guid condominiumId = Guid.CreateVersion7();
        Guid chargeId = Guid.CreateVersion7();
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

        await new HangfireInvoiceDeliveryDispatcher(client.Object).EnqueueAsync(condominiumId, chargeId, default);

        job!.Type.Should().Be<SendInvoiceJob>();
        job.Method.Name.Should().Be(nameof(SendInvoiceJob.RunAsync));
        job.Args.Take(2).Should().Equal(condominiumId, chargeId);
        job.Args.Should().HaveCount(3);
        state.Should().BeOfType<EnqueuedState>();
    }

    [Fact]
    public async Task RunAsync_ChoosesTheCondominiumBeforeAnythingIsRead_ThenSendsTheCommand()
    {
        Mock<ITenantScope> tenant = new(MockBehavior.Strict);
        Mock<ISender> sender = new(MockBehavior.Strict);
        Guid condominiumId = Guid.CreateVersion7();
        Guid chargeId = Guid.CreateVersion7();
        MockSequence sequence = new();
        tenant.InSequence(sequence).Setup(scope => scope.Enter(condominiumId));
        sender.InSequence(sequence).Setup(s => s.Send(It.Is<SendInvoiceCommand>(c => c.ChargeId == chargeId), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        await new SendInvoiceJob(tenant.Object, sender.Object).RunAsync(condominiumId, chargeId, default);

        tenant.VerifyAll();
        sender.VerifyAll();
    }

    [Fact]
    public async Task RunAsync_LetsTheProviderFailureReachHangfireSoTheRetryPolicyApplies()
    {
        Mock<ISender> sender = new();
        sender
            .Setup(s => s.Send(It.IsAny<SendInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("falhou"));

        Func<Task> run = () => new SendInvoiceJob(Mock.Of<ITenantScope>(), sender.Object).RunAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
    }

    [Fact]
    public void RunAsync_RetriesFiveTimesWithGrowingDelaysAndThenFails()
    {
        AutomaticRetryAttribute retry = typeof(SendInvoiceJob).GetMethod(nameof(SendInvoiceJob.RunAsync))!.GetCustomAttribute<AutomaticRetryAttribute>()!;

        retry.Attempts.Should().Be(5);
        retry.DelaysInSeconds.Should().Equal(60, 300, 900, 3600, 10800);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }
}
