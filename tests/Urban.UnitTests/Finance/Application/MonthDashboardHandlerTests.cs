using FluentAssertions;
using Urban.Finance.Application.Dashboard;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

/// <summary>O painel financeiro do mês. Hoje é 7/11/2026; outubro tem um lançamento de R$ 1.000,00 e, fechado, duas cobranças, 600 e 400.</summary>
public class MonthDashboardHandlerTests
{
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 7);

    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _contracts;

    public MonthDashboardHandlerTests()
    {
        _contracts = _kit.NewGroup("Contratos");
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private GetMonthDashboardQueryHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new(used.Entries, used.Closings, used.Groups, used.Payments, used.Tenant, used.Clock);
    }

    private Task<Result<MonthDashboardView>> Get(string month = "2026-10") => Handler().Handle(new GetMonthDashboardQuery(month), default);

    private void OpenOctober(decimal amount = 1000m)
    {
        Expense expense = _kit.NewFixed(_contracts, M(2026, 10), amount, "Portaria");
        _kit.NewEntry(expense, M(2026, 10));
    }

    private (UnitCharge First, UnitCharge Second) CloseOctober(DateOnly? dueDate = null)
    {
        OpenOctober();
        MonthClosing closing = _kit.CloseMonth(M(2026, 10), dueDate);
        List<UnitCharge> charges = [.. closing.Charges.OrderBy(charge => charge.Position)];

        return (charges[0], charges[1]);
    }

    private Payment Pay(UnitCharge charge, decimal amount) =>
        _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, charge.Id, M(2026, 10), amount, new DateOnly(2026, 11, 5), PaymentMethod.Pix, null, Manager, FinanceTestKit.Now, Today).Value);

    // ---- Pedido ----

    [Fact]
    public async Task WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<MonthDashboardView> result = await Handler(kit).Handle(new GetMonthDashboardQuery("2026-10"), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("2026-13")]
    [InlineData("2026/10")]
    public async Task AnInvalidMonth_Fails(string? month)
    {
        Result<MonthDashboardView> result = await Handler().Handle(new GetMonthDashboardQuery(month!), default);

        result.Error.Should().Be(MonthErrors.Invalid);
    }

    // ---- Despesas ----

    [Fact]
    public async Task AMonthThatWasNeverOpened_IsEmptyAndNotClosed()
    {
        Result<MonthDashboardView> result = await Get("2026-12");

        result.IsSuccess.Should().BeTrue();
        result.Value.Month.Should().Be("2026-12");
        result.Value.AsOf.Should().Be(Today);
        result.Value.Expenses.Total.Should().Be(0m);
        result.Value.Expenses.NeedsAmountCount.Should().Be(0);
        result.Value.Expenses.OverdueCount.Should().Be(0);
        result.Value.Expenses.Groups.Should().BeEmpty();
        result.Value.Charges.Should().BeNull();
    }

    [Fact]
    public async Task AnOpenMonth_ShowsTheExpensesByGroup_AndNoCharges()
    {
        ExpenseGroup works = _kit.NewGroup("Obras");
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 11), 700m, "Portaria"), M(2026, 11));
        _kit.NewEntry(_kit.NewFixed(works, M(2026, 11), 300m, "Pintura"), M(2026, 11));
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 11), 50m, "Limpeza"), M(2026, 11));

        Result<MonthDashboardView> result = await Get("2026-11");

        result.Value.Expenses.Total.Should().Be(1050m);
        result.Value.Expenses.Groups.Select(group => (group.GroupName, group.Total)).Should().Equal(("Contratos", 750m), ("Obras", 300m));
        result.Value.Charges.Should().BeNull();
    }

    [Fact]
    public async Task TheExpenses_LeaveCanceledOnesOut_AndCountTheOnesWaitingForAValueAndTheOverdueOnes()
    {
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 10), 400m, "Portaria"), M(2026, 10));
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 10), 100m, "Cancelada"), M(2026, 10)).SetStatus(EntryStatus.Canceled);
        _kit.NewEntry(_kit.NewVariable(_contracts, M(2026, 10), "Energia elétrica"), M(2026, 10));
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 10), 200m, "Paga"), M(2026, 10)).SetStatus(EntryStatus.Paid);

        Result<MonthDashboardView> result = await Get();

        result.Value.Expenses.Total.Should().Be(600m);
        result.Value.Expenses.NeedsAmountCount.Should().Be(1);
        result.Value.Expenses.OverdueCount.Should().Be(2);
        result.Value.Expenses.Groups.Should().ContainSingle().Which.Total.Should().Be(600m);
    }

    // ---- Cobranças ----

    [Fact]
    public async Task AClosedMonth_WithNothingPaid_IsAllOpen()
    {
        CloseOctober();

        Result<MonthDashboardView> result = await Get();

        DashboardChargesView charges = result.Value.Charges!;
        charges.DueDate.Should().Be(new DateOnly(2026, 11, 10));
        charges.Charged.Should().Be(1000m);
        charges.Received.Should().Be(0m);
        charges.Balance.Should().Be(1000m);
        charges.Units.Should().Be(2);
        (charges.Open, charges.Partial, charges.Paid, charges.Overdue).Should().Be((2, 0, 0, 0));
        charges.OverdueAmount.Should().Be(0m);
        result.Value.Expenses.Total.Should().Be(1000m);
    }

    [Fact]
    public async Task PaymentsMoveEachUnitToPaidOrPartial_AndLowerTheBalance()
    {
        (UnitCharge first, UnitCharge second) = CloseOctober();
        Pay(first, 600m);
        Pay(second, 100m);

        DashboardChargesView charges = (await Get()).Value.Charges!;

        charges.Received.Should().Be(700m);
        charges.Balance.Should().Be(300m);
        (charges.Open, charges.Partial, charges.Paid, charges.Overdue).Should().Be((0, 1, 1, 0));
        charges.OverdueAmount.Should().Be(0m);
    }

    [Fact]
    public async Task SeveralPaymentsOnTheSameCharge_AreAdded()
    {
        (UnitCharge first, _) = CloseOctober();
        Pay(first, 200m);
        Pay(first, 400m);

        DashboardChargesView charges = (await Get()).Value.Charges!;

        charges.Received.Should().Be(600m);
        (charges.Open, charges.Partial, charges.Paid, charges.Overdue).Should().Be((1, 0, 1, 0));
    }

    [Fact]
    public async Task AfterTheDueDate_WhatIsNotFullyPaidIsOverdue_PartialOnesIncluded()
    {
        (UnitCharge first, UnitCharge second) = CloseOctober(new DateOnly(2026, 11, 1));
        Pay(first, 150m);

        DashboardChargesView charges = (await Get()).Value.Charges!;

        (charges.Open, charges.Partial, charges.Paid, charges.Overdue).Should().Be((0, 0, 0, 2));
        charges.OverdueAmount.Should().Be(450m + second.Amount);
        charges.Balance.Should().Be(850m);
    }

    [Fact]
    public async Task OverdueAmount_CountsOnlyTheBalanceOfTheOverdueCharges()
    {
        (UnitCharge first, UnitCharge second) = CloseOctober(new DateOnly(2026, 11, 1));
        Pay(first, 600m);

        DashboardChargesView charges = (await Get()).Value.Charges!;

        (charges.Open, charges.Partial, charges.Paid, charges.Overdue).Should().Be((0, 0, 1, 1));
        charges.OverdueAmount.Should().Be(second.Amount);
        charges.Received.Should().Be(600m);
    }

    [Fact]
    public async Task OnTheDueDateItself_NothingIsOverdueYet()
    {
        CloseOctober(Today);

        DashboardChargesView charges = (await Get()).Value.Charges!;

        (charges.Open, charges.Overdue).Should().Be((2, 0));
        charges.OverdueAmount.Should().Be(0m);
    }

    [Fact]
    public async Task AReversedPayment_NoLongerCounts()
    {
        (UnitCharge first, _) = CloseOctober(new DateOnly(2026, 11, 1));
        Pay(first, 600m).Reverse("Unidade errada", Manager, FinanceTestKit.Now);

        DashboardChargesView charges = (await Get()).Value.Charges!;

        charges.Received.Should().Be(0m);
        (charges.Paid, charges.Overdue).Should().Be((0, 2));
        charges.OverdueAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task TheCharges_AreOnlyThoseOfTheRequestedMonth()
    {
        (UnitCharge first, _) = CloseOctober(new DateOnly(2026, 11, 1));
        Pay(first, 600m);
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 9), 500m, "Antiga"), M(2026, 9));
        _kit.CloseMonth(M(2026, 9), new DateOnly(2026, 10, 10));

        DashboardChargesView october = (await Get()).Value.Charges!;
        DashboardChargesView september = (await Get("2026-09")).Value.Charges!;

        october.Charged.Should().Be(1000m);
        october.Received.Should().Be(600m);
        september.Charged.Should().Be(500m);
        september.Received.Should().Be(0m);
        september.Overdue.Should().Be(2);
    }
}
