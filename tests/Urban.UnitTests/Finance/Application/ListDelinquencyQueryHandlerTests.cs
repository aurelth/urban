using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Delinquency;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// A inadimplência: as unidades com cobrança vencida e não paga, de todos os meses fechados. Hoje é 7/11/2026; cada mês fechado
/// tem duas cobranças, 600 (unidade 101) e 400 (A-102).
/// </summary>
public class ListDelinquencyQueryHandlerTests
{
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 7);

    private readonly FinanceTestKit _kit = new();
    private readonly Expense _expense;

    public ListDelinquencyQueryHandlerTests()
    {
        _expense = _kit.NewFixed(_kit.NewGroup(), M(2026, 6), 1000m);
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private ListDelinquencyQueryHandler Handler() => new(_kit.Overdue, _kit.Tenant, _kit.Clock);

    private async Task<DelinquencyView> ListAsync(int page = 1, int pageSize = 50)
    {
        Result<DelinquencyView> result = await Handler().Handle(new ListDelinquencyQuery(page, pageSize), default);

        result.IsSuccess.Should().BeTrue();

        return result.Value;
    }

    private MonthClosing Close(YearMonth month, DateOnly dueDate)
    {
        _kit.NewEntry(_expense, month);

        return _kit.CloseMonth(month, dueDate);
    }

    private static UnitCharge Big(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).First();

    private static UnitCharge Small(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).Last();

    private Payment Paid(MonthClosing closing, UnitCharge charge, decimal amount) =>
        _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, charge.Id, closing.Month, amount, new DateOnly(2026, 11, 5), PaymentMethod.Pix, null, Manager, FinanceTestKit.Now, Today).Value);

    // ---- Pedido ----

    [Fact]
    public async Task WithoutACondominium_Fails_AndReadsNothing()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<DelinquencyView> result = await new ListDelinquencyQueryHandler(kit.Overdue, kit.Tenant, kit.Clock).Handle(new ListDelinquencyQuery(), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Overdue.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task APageOrSizeOutOfRange_IsRefused_AndReadsNothing(int page, int pageSize)
    {
        Result<DelinquencyView> result = await Handler().Handle(new ListDelinquencyQuery(page, pageSize), default);

        result.Error.Should().Be(DelinquencyErrors.PageInvalid);
        _kit.Overdue.Calls.Should().Be(0);
    }

    // ---- O que está em atraso ----

    [Fact]
    public async Task WithNothingOverdue_ReturnsAnEmptyListWithZeroTotals()
    {
        Close(M(2026, 10), new DateOnly(2026, 11, 10));

        DelinquencyView view = await ListAsync();

        view.AsOf.Should().Be(Today);
        view.TotalOverdue.Should().Be(0m);
        view.Units.Should().Be(0);
        view.Items.Should().BeEmpty();
        view.Page.Should().Be(1);
        view.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task WithNoClosedMonth_ReturnsAnEmptyList()
    {
        DelinquencyView view = await ListAsync();

        view.Items.Should().BeEmpty();
        view.TotalOverdue.Should().Be(0m);
    }

    [Fact]
    public async Task TheDueDateItselfStillCounts_SoACharge_IsOverdueOnlyFromTheNextDay()
    {
        Close(M(2026, 9), Today);

        (await ListAsync()).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AChargeThatExpiredYesterday_IsOneDayOverdue()
    {
        Close(M(2026, 9), Today.AddDays(-1));

        DelinquencyView view = await ListAsync();

        view.Units.Should().Be(2);
        view.Items.Select(item => item.DaysOverdue).Should().OnlyContain(days => days == 1);
    }

    [Fact]
    public async Task AChargePaidInFull_IsNotListed()
    {
        MonthClosing closing = Close(M(2026, 9), new DateOnly(2026, 10, 10));
        Paid(closing, Big(closing), 600m);

        DelinquencyView view = await ListAsync();

        view.Items.Should().ContainSingle().Which.Number.Should().Be("102");
        view.TotalOverdue.Should().Be(400m);
    }

    [Fact]
    public async Task APartialPayment_LeavesOnlyTheBalanceOverdue()
    {
        MonthClosing closing = Close(M(2026, 9), new DateOnly(2026, 10, 10));
        Paid(closing, Big(closing), 250m);

        DebtorView debtor = (await ListAsync()).Items.First(item => item.Number == "101");

        debtor.Balance.Should().Be(350m);
        debtor.Charges.Should().ContainSingle();
        debtor.Charges[0].Amount.Should().Be(600m);
        debtor.Charges[0].Paid.Should().Be(250m);
        debtor.Charges[0].Balance.Should().Be(350m);
    }

    [Fact]
    public async Task AReversedPayment_DoesNotCountAsPaid()
    {
        MonthClosing closing = Close(M(2026, 9), new DateOnly(2026, 10, 10));
        Payment wrong = Paid(closing, Big(closing), 600m);
        wrong.Reverse("Unidade errada", Manager, FinanceTestKit.Now);

        DebtorView debtor = (await ListAsync()).Items.First(item => item.Number == "101");

        debtor.Balance.Should().Be(600m);
    }

    [Fact]
    public async Task APortThatReturnsAChargeNotYetOverdue_IsCheckedAgainstTheRuleOfTheDomain()
    {
        Guid unit = Guid.CreateVersion7();
        OverdueRow notYet = new(Guid.CreateVersion7(), unit, null, "101", 0, M(2026, 10), Today, 600m, 0m);
        ListDelinquencyQueryHandler handler = new(new StaticOverdueCharges(notYet), _kit.Tenant, _kit.Clock);

        Result<DelinquencyView> result = await handler.Handle(new ListDelinquencyQuery(), default);

        result.Value.Items.Should().BeEmpty();
        result.Value.TotalOverdue.Should().Be(0m);
    }

    // ---- Por unidade ----

    [Fact]
    public async Task SeveralMonthsOfTheSameUnit_AreGroupedWithTheTotalTheOldestDueDateAndTheDaysOverdue()
    {
        Close(M(2026, 8), new DateOnly(2026, 9, 10));
        Close(M(2026, 9), new DateOnly(2026, 10, 10));

        DebtorView debtor = (await ListAsync()).Items.First(item => item.Number == "101");

        debtor.Balance.Should().Be(1200m);
        debtor.OverdueCharges.Should().Be(2);
        debtor.OldestDueDate.Should().Be(new DateOnly(2026, 9, 10));
        debtor.DaysOverdue.Should().Be(58);
        debtor.Charges.Select(charge => charge.Month).Should().Equal("2026-08", "2026-09");
        debtor.Charges.Select(charge => charge.DueDate).Should().Equal(new DateOnly(2026, 9, 10), new DateOnly(2026, 10, 10));
        debtor.Charges.Select(charge => charge.DaysOverdue).Should().Equal(58, 28);
        debtor.Charges.Select(charge => charge.Balance).Should().Equal(600m, 600m);
    }

    [Fact]
    public async Task TheUnitIsIdentifiedByItsBlockAndNumber()
    {
        Close(M(2026, 9), new DateOnly(2026, 10, 10));

        DebtorView[] items = [.. (await ListAsync()).Items];

        items.Select(item => (item.Block, item.Number)).Should().BeEquivalentTo([((string?)null, "101"), ("A", "102")]);
        items.Select(item => item.UnitId).Should().BeEquivalentTo([FakeApportionmentBasis.First.UnitId, FakeApportionmentBasis.Second.UnitId]);
    }

    [Fact]
    public async Task AUnitThatPaidEverything_DoesNotAppearEvenWithOldMonthsOnTheList()
    {
        MonthClosing august = Close(M(2026, 8), new DateOnly(2026, 9, 10));
        Paid(august, Big(august), 600m);
        MonthClosing september = Close(M(2026, 9), new DateOnly(2026, 10, 10));
        Paid(september, Big(september), 600m);

        DelinquencyView view = await ListAsync();

        view.Items.Should().ContainSingle().Which.Number.Should().Be("102");
        view.Items[0].Balance.Should().Be(800m);
    }

    // ---- Ordem e página ----

    [Fact]
    public async Task TheUnitThatOwesMoreComesFirst()
    {
        MonthClosing closing = Close(M(2026, 9), new DateOnly(2026, 10, 10));
        Paid(closing, Big(closing), 250m);

        DelinquencyView view = await ListAsync();

        view.Items.Select(item => item.Number).Should().Equal("102", "101");
        view.Items.Select(item => item.Balance).Should().Equal(400m, 350m);
    }

    [Fact]
    public async Task WithTheSameDebt_TheUnitsKeepTheirOrder()
    {
        MonthClosing august = Close(M(2026, 8), new DateOnly(2026, 9, 10));
        Paid(august, Big(august), 200m);

        DelinquencyView tie = await ListAsync();

        tie.Items.Select(item => item.Balance).Should().Equal(400m, 400m);
        tie.Items.Select(item => item.Number).Should().Equal("101", "102");
    }

    [Fact]
    public async Task TheTotalsCoverEveryDebtor_NotJustThePage()
    {
        Close(M(2026, 9), new DateOnly(2026, 10, 10));

        DelinquencyView second = await ListAsync(page: 2, pageSize: 1);

        second.Items.Should().ContainSingle().Which.Number.Should().Be("102");
        second.TotalOverdue.Should().Be(1000m);
        second.Units.Should().Be(2);
        second.Page.Should().Be(2);
        second.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task APagePastTheEnd_IsEmpty_ButKeepsTheTotals()
    {
        Close(M(2026, 9), new DateOnly(2026, 10, 10));

        DelinquencyView view = await ListAsync(page: 3, pageSize: 1);

        view.Items.Should().BeEmpty();
        view.Units.Should().Be(2);
        view.TotalOverdue.Should().Be(1000m);
    }

    private sealed class StaticOverdueCharges(params OverdueRow[] rows) : IOverdueCharges
    {
        public Task<IReadOnlyList<OverdueRow>> ListAsync(DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OverdueRow>>(rows);
    }
}
