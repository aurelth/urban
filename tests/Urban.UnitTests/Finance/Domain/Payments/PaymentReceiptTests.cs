using FluentAssertions;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain.Payments;

/// <summary>O número do recibo de um pagamento: nasce vazio, a gravação o emite uma vez e ele nunca mais muda.</summary>
public class PaymentReceiptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static Payment NewPayment() => Payment.Register(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        YearMonth.Create(2026, 10).Value,
        100m,
        new DateOnly(2026, 11, 5),
        PaymentMethod.Pix,
        null,
        Guid.CreateVersion7(),
        Now,
        new DateOnly(2026, 11, 7)).Value;

    [Fact]
    public void ANewPayment_HasNoReceiptNumberYet()
    {
        NewPayment().ReceiptNumber.Should().Be(0);
    }

    [Fact]
    public void IssueReceipt_GivesThePaymentItsNumber()
    {
        Payment payment = NewPayment();

        Result result = payment.IssueReceipt(7);

        result.IsSuccess.Should().BeTrue();
        payment.ReceiptNumber.Should().Be(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IssueReceipt_RefusesANumberBelowOne_AndKeepsTheOldState(int number)
    {
        Payment payment = NewPayment();

        Result result = payment.IssueReceipt(number);

        result.Error.Should().Be(PaymentErrors.ReceiptNumberInvalid);
        payment.ReceiptNumber.Should().Be(0);
    }

    [Fact]
    public void IssueReceipt_ASecondTime_IsRefused_AndTheFirstNumberStays()
    {
        Payment payment = NewPayment();
        payment.IssueReceipt(3);

        Result result = payment.IssueReceipt(4);

        result.Error.Should().Be(PaymentErrors.ReceiptAlreadyIssued);
        payment.ReceiptNumber.Should().Be(3);
    }

    [Fact]
    public void AReversal_KeepsTheReceiptNumber()
    {
        Payment payment = NewPayment();
        payment.IssueReceipt(3);

        payment.Reverse("Unidade errada", Guid.CreateVersion7(), Now.AddDays(1)).IsSuccess.Should().BeTrue();

        payment.ReceiptNumber.Should().Be(3);
        payment.IsReversed.Should().BeTrue();
    }
}
