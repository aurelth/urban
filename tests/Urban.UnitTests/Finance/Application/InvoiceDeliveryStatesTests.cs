using FluentAssertions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Invoices;

namespace Urban.UnitTests.Finance.Application;

public class InvoiceDeliveryStatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static InvoiceDelivery Queued(DateTimeOffset requestedAt) => InvoiceDelivery.Queue(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), requestedAt);

    private static InvoiceDelivery Sent()
    {
        InvoiceDelivery delivery = Queued(Now.AddDays(-1));
        delivery.MarkSent(Now.AddDays(-1));

        return delivery;
    }

    private static InvoiceDelivery Failed()
    {
        InvoiceDelivery delivery = Queued(Now.AddHours(-1));
        delivery.MarkFailed(InvoiceDeliveryFailure.NotDelivered, Now.AddMinutes(-30));

        return delivery;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithoutADelivery_ItIsNotSentWhenThereIsAnEmail_AndNoRecipientWhenThereIsNot(bool hasEmail)
    {
        InvoiceDeliveryStates.Of(null, hasEmail, Now).Should().Be(hasEmail ? InvoiceDeliveryState.NotSent : InvoiceDeliveryState.NoRecipient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASentInvoiceStaysSent_EvenIfTheResponsibleLostTheEmailLater(bool hasEmail)
    {
        InvoiceDeliveryStates.Of(Sent(), hasEmail, Now).Should().Be(InvoiceDeliveryState.Sent);
    }

    [Fact]
    public void AnInvoiceInTheQueueIsQueued_EvenWithoutEmail()
    {
        InvoiceDeliveryStates.Of(Queued(Now.AddMinutes(-5)), hasEmail: false, Now).Should().Be(InvoiceDeliveryState.Queued);
    }

    [Fact]
    public void AnInvoiceLostInTheQueueCountsAsNotSent()
    {
        InvoiceDeliveryStates.Of(Queued(Now - InvoiceDelivery.LostAfter), hasEmail: true, Now).Should().Be(InvoiceDeliveryState.NotSent);
    }

    [Fact]
    public void AFailedInvoiceIsFailed_UnlessThereIsNoEmailToTryAgain()
    {
        InvoiceDeliveryStates.Of(Failed(), hasEmail: true, Now).Should().Be(InvoiceDeliveryState.Failed);
        InvoiceDeliveryStates.Of(Failed(), hasEmail: false, Now).Should().Be(InvoiceDeliveryState.NoRecipient);
    }
}
