using FluentAssertions;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain.Payments;

public class ChargeAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 7);

    private static Payment Pay(decimal amount) =>
        Payment.Register(Guid.CreateVersion7(), Guid.CreateVersion7(), YearMonth.Create(2026, 11).Value, amount, Today, PaymentMethod.Pix, null, Guid.CreateVersion7(), Now, Today).Value;

    [Fact]
    public void Paid_SumsThePaymentsThatWereNotReversed()
    {
        Payment first = Pay(100m);
        Payment second = Pay(50.25m);
        Payment reversed = Pay(70m);
        reversed.Reverse("Erro", Guid.CreateVersion7(), Now);

        ChargeAccount.Paid([first, second, reversed]).Should().Be(150.25m);
    }

    [Fact]
    public void Paid_IsZeroWithNoPayments()
    {
        ChargeAccount.Paid([]).Should().Be(0m);
    }

    [Fact]
    public void Balance_IsWhatIsStillOwed()
    {
        ChargeAccount.Balance(500m, 300m).Should().Be(200m);
        ChargeAccount.Balance(500m, 500m).Should().Be(0m);
    }

    [Theory]
    [InlineData(500, 0, "2026-11-10", "2026-11-07", ChargeStatus.Open)]
    [InlineData(500, 0, "2026-11-10", "2026-11-10", ChargeStatus.Open)]
    [InlineData(500, 0, "2026-11-10", "2026-11-11", ChargeStatus.Overdue)]
    [InlineData(500, 300, "2026-11-10", "2026-11-07", ChargeStatus.Partial)]
    [InlineData(500, 300, "2026-11-10", "2026-11-10", ChargeStatus.Partial)]
    [InlineData(500, 300, "2026-11-10", "2026-11-11", ChargeStatus.Overdue)]
    [InlineData(500, 500, "2026-11-10", "2026-11-07", ChargeStatus.Paid)]
    [InlineData(500, 500, "2026-11-10", "2026-12-30", ChargeStatus.Paid)]
    public void StatusOf_IsCalculatedNeverStored(int amount, int paid, string due, string today, ChargeStatus expected)
    {
        ChargeAccount.StatusOf(amount, paid, DateOnly.Parse(due), DateOnly.Parse(today)).Should().Be(expected);
    }

    [Fact]
    public void EnsureFits_AcceptsUpToTheBalanceExactly()
    {
        ChargeAccount.EnsureFits(500m, 300m, 200m).IsSuccess.Should().BeTrue();
        ChargeAccount.EnsureFits(500m, 0m, 500m).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EnsureFits_RefusesAnOverpaymentAndSaysHowMuchIsLeft()
    {
        Result result = ChargeAccount.EnsureFits(500m, 300m, 200.01m);

        result.Error.Code.Should().Be(PaymentErrors.ExceedsBalance(200m).Code);
        result.Error.Data!["balance"].Should().Be(200m);
    }

    [Fact]
    public void EnsureFits_RefusesAnyPaymentOnAChargeThatIsAlreadyPaid()
    {
        ChargeAccount.EnsureFits(500m, 500m, 0.01m).Error.Data!["balance"].Should().Be(0m);
    }
}
