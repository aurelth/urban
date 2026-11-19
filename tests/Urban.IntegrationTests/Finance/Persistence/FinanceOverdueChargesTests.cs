using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceOverdueChargesTests(FinancePostgresFixture postgres)
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    private readonly FinanceStoreKit _kit = new(postgres);

    private static YearMonth August => FinanceStoreKit.M(2026, 8);

    private static YearMonth September => FinanceStoreKit.M(2026, 9);

    private static YearMonth October => FinanceStoreKit.M(2026, 10);

    private static UnitCharge Big(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).First();

    private static UnitCharge Small(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).Last();

    private async Task<Payment> StorePaymentAsync(UnitCharge charge, YearMonth month, decimal amount)
    {
        Payment payment = FinanceStoreKit.NewPayment(charge, month, amount);
        (await _kit.Payments().AddAsync(payment, charge.Amount, default)).IsSuccess.Should().BeTrue();

        return payment;
    }

    [Fact]
    public async Task List_ReturnsTheUnpaidChargesOfTheClosedMonthsThatAlreadyExpired()
    {
        MonthClosing august = await _kit.StoreClosingAsync(August, new DateOnly(2026, 9, 10));
        MonthClosing september = await _kit.StoreClosingAsync(September, new DateOnly(2026, 10, 10));
        await _kit.StoreClosingAsync(October, new DateOnly(2026, 11, 10));

        IReadOnlyList<OverdueRow> rows = await _kit.Overdue().ListAsync(Today, default);

        rows.Select(row => row.ChargeId).Should().BeEquivalentTo([.. august.Charges.Select(charge => charge.Id), .. september.Charges.Select(charge => charge.Id)]);
        OverdueRow first = rows.Single(row => row.ChargeId == Big(august).Id);
        first.UnitId.Should().Be(Big(august).UnitId);
        first.Block.Should().BeNull();
        first.Number.Should().Be("101");
        first.Position.Should().Be(Big(august).Position);
        first.Month.Should().Be(August);
        first.DueDate.Should().Be(new DateOnly(2026, 9, 10));
        first.Amount.Should().Be(600m);
        first.Paid.Should().Be(0m);
        OverdueRow second = rows.Single(row => row.ChargeId == Small(august).Id);
        second.Block.Should().Be("A");
        second.Number.Should().Be("102");
        second.Amount.Should().Be(400m);
    }

    [Fact]
    public async Task List_TheDueDateOfToday_IsNotOverdueYet()
    {
        await _kit.StoreClosingAsync(October, Today);

        (await _kit.Overdue().ListAsync(Today, default)).Should().BeEmpty();
        (await _kit.Overdue().ListAsync(Today.AddDays(1), default)).Should().HaveCount(2);
    }

    [Fact]
    public async Task List_SumsOnlyThePaymentsThatWereNotReversed()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(September, new DateOnly(2026, 10, 10));
        UnitCharge charge = Big(closing);
        await StorePaymentAsync(charge, September, 200m);
        await StorePaymentAsync(charge, September, 50m);
        Payment wrong = await StorePaymentAsync(charge, September, 100m);
        Payment loaded = (await _kit.Payments().FindAsync(wrong.Id, default))!;
        loaded.Reverse("Unidade errada", Guid.CreateVersion7(), FinanceStoreKit.Now);
        (await _kit.Payments().ReverseAsync(loaded, default)).IsSuccess.Should().BeTrue();

        IReadOnlyList<OverdueRow> rows = await _kit.Overdue().ListAsync(Today, default);

        rows.Single(row => row.ChargeId == charge.Id).Paid.Should().Be(250m);
    }

    [Fact]
    public async Task List_LeavesOutTheChargesPaidInFull()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(September, new DateOnly(2026, 10, 10));
        await StorePaymentAsync(Big(closing), September, 400m);
        await StorePaymentAsync(Big(closing), September, 200m);

        IReadOnlyList<OverdueRow> rows = await _kit.Overdue().ListAsync(Today, default);

        rows.Select(row => row.ChargeId).Should().Equal(Small(closing).Id);
    }

    [Fact]
    public async Task List_AFullyPaidChargeWhoseOnlyPaymentWasReversed_IsOverdueAgain()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(September, new DateOnly(2026, 10, 10));
        Payment payment = await StorePaymentAsync(Big(closing), September, 600m);
        Payment loaded = (await _kit.Payments().FindAsync(payment.Id, default))!;
        loaded.Reverse("Pagamento duplicado", Guid.CreateVersion7(), FinanceStoreKit.Now);
        (await _kit.Payments().ReverseAsync(loaded, default)).IsSuccess.Should().BeTrue();

        IReadOnlyList<OverdueRow> rows = await _kit.Overdue().ListAsync(Today, default);

        rows.Should().HaveCount(2);
        rows.Single(row => row.ChargeId == Big(closing).Id).Paid.Should().Be(0m);
    }

    [Fact]
    public async Task List_NeverShowsTheChargesOfAnotherCondominium()
    {
        await _kit.StoreClosingAsync(September, new DateOnly(2026, 10, 10));
        Guid other = Guid.CreateVersion7();

        (await _kit.Overdue(other).ListAsync(Today, default)).Should().BeEmpty();
        (await _kit.Overdue().ListAsync(Today, default)).Should().HaveCount(2);
    }
}
