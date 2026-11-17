using FluentAssertions;
using Urban.Finance.Domain.Invoices;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class InvoiceDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Condominium = Guid.CreateVersion7();
    private static readonly Guid Charge = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();

    private static InvoiceDelivery NewDelivery() => InvoiceDelivery.Queue(Condominium, Charge, Actor, Now);

    [Fact]
    public void Queue_StartsQueuedWithoutAttemptsAndRemembersWhoAskedAndWhen()
    {
        InvoiceDelivery delivery = NewDelivery();

        delivery.Id.Should().NotBeEmpty();
        delivery.CondominiumId.Should().Be(Condominium);
        delivery.ChargeId.Should().Be(Charge);
        delivery.Status.Should().Be(InvoiceDeliveryStatus.Queued);
        delivery.Attempts.Should().Be(0);
        delivery.RequestedBy.Should().Be(Actor);
        delivery.RequestedAt.Should().Be(Now);
        delivery.LastAttemptAt.Should().BeNull();
        delivery.SentAt.Should().BeNull();
        delivery.FailureReason.Should().BeNull();
    }

    [Fact]
    public void MarkSent_RecordsTheAttemptAndWhenItLeft()
    {
        InvoiceDelivery delivery = NewDelivery();

        delivery.MarkSent(Now.AddMinutes(1));

        delivery.Status.Should().Be(InvoiceDeliveryStatus.Sent);
        delivery.Attempts.Should().Be(1);
        delivery.LastAttemptAt.Should().Be(Now.AddMinutes(1));
        delivery.SentAt.Should().Be(Now.AddMinutes(1));
        delivery.FailureReason.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_RecordsTheAttemptAndTheReason_AndASuccessfulRetryClearsIt()
    {
        InvoiceDelivery delivery = NewDelivery();

        delivery.MarkFailed(InvoiceDeliveryFailure.NotDelivered, Now.AddMinutes(1));

        delivery.Status.Should().Be(InvoiceDeliveryStatus.Failed);
        delivery.Attempts.Should().Be(1);
        delivery.LastAttemptAt.Should().Be(Now.AddMinutes(1));
        delivery.FailureReason.Should().Be("not_delivered");
        delivery.SentAt.Should().BeNull();

        delivery.MarkSent(Now.AddMinutes(6));

        delivery.Status.Should().Be(InvoiceDeliveryStatus.Sent);
        delivery.Attempts.Should().Be(2);
        delivery.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Requeue_AfterASend_StartsANewRequest_ButKeepsWhenItLastLeft()
    {
        InvoiceDelivery delivery = NewDelivery();
        delivery.MarkSent(Now.AddMinutes(1));
        Guid other = Guid.CreateVersion7();

        Result result = delivery.Requeue(other, Now.AddDays(1));

        result.IsSuccess.Should().BeTrue();
        delivery.Status.Should().Be(InvoiceDeliveryStatus.Queued);
        delivery.Attempts.Should().Be(0);
        delivery.RequestedBy.Should().Be(other);
        delivery.RequestedAt.Should().Be(Now.AddDays(1));
        delivery.SentAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Requeue_AfterAFailure_ClearsTheReason()
    {
        InvoiceDelivery delivery = NewDelivery();
        delivery.MarkFailed(InvoiceDeliveryFailure.NoRecipient, Now.AddMinutes(1));

        delivery.Requeue(Actor, Now.AddHours(1)).IsSuccess.Should().BeTrue();

        delivery.FailureReason.Should().BeNull();
        delivery.Status.Should().Be(InvoiceDeliveryStatus.Queued);
    }

    [Fact]
    public void Requeue_WhileStillInTheQueue_IsRefused()
    {
        InvoiceDelivery delivery = NewDelivery();

        Result result = delivery.Requeue(Guid.CreateVersion7(), Now.AddHours(1));

        result.Error.Should().Be(InvoiceDeliveryErrors.AlreadyQueued);
        delivery.RequestedAt.Should().Be(Now);
        delivery.RequestedBy.Should().Be(Actor);
    }

    [Fact]
    public void Requeue_OfAnInvoiceLostInTheQueue_IsAllowedOnceTheLimitPasses()
    {
        InvoiceDelivery delivery = NewDelivery();

        delivery.IsInFlight(Now.AddTicks(InvoiceDelivery.LostAfter.Ticks - 1)).Should().BeTrue();
        delivery.IsInFlight(Now + InvoiceDelivery.LostAfter).Should().BeFalse();
        delivery.Requeue(Actor, Now + InvoiceDelivery.LostAfter).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TheLostLimit_IsLongerThanTheWholeRetryLadder()
    {
        TimeSpan ladder = TimeSpan.FromSeconds(60 + 300 + 900 + 3600 + 10800);

        InvoiceDelivery.LostAfter.Should().BeGreaterThan(ladder);
    }

    [Theory]
    [InlineData(InvoiceDeliveryStatus.Sent)]
    [InlineData(InvoiceDeliveryStatus.Failed)]
    public void OnlyAQueuedInvoiceIsInFlight(InvoiceDeliveryStatus status)
    {
        InvoiceDelivery delivery = NewDelivery();

        if (status == InvoiceDeliveryStatus.Sent)
        {
            delivery.MarkSent(Now);
        }
        else
        {
            delivery.MarkFailed(InvoiceDeliveryFailure.NotDelivered, Now);
        }

        delivery.IsInFlight(Now).Should().BeFalse();
    }

    [Fact]
    public void TheFailureReasonsFitTheColumn()
    {
        new[] { InvoiceDeliveryFailure.NoRecipient, InvoiceDeliveryFailure.NotDelivered, InvoiceDeliveryFailure.InvoiceUnavailable }
            .Should().OnlyContain(reason => reason.Length <= InvoiceDelivery.FailureReasonMaxLength);
    }
}
