using FluentAssertions;
using Urban.Finance.Application.MyMonth;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// O mês financeiro de quem mora no condomínio. Hoje é 7/11/2026; outubro tem um lançamento de R$ 1.000,00 e, fechado, duas cobranças:
/// a da unidade 101 (600) e a da 102 do bloco A (400). O morador vê a cobrança das unidades dele e as despesas por grupo, e nada do
/// que é do síndico: fornecedor, observação, inadimplência e o que os outros pagaram.
/// </summary>
public class GetMyMonthQueryHandlerTests
{
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly Guid Resident = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 7);
    private static readonly ResidentUnit Unit101 = new(FakeApportionmentBasis.First.UnitId, FakeApportionmentBasis.First.Block, FakeApportionmentBasis.First.Number);
    private static readonly ResidentUnit Unit102 = new(FakeApportionmentBasis.Second.UnitId, FakeApportionmentBasis.Second.Block, FakeApportionmentBasis.Second.Number);

    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _contracts;

    public GetMyMonthQueryHandlerTests()
    {
        _contracts = _kit.NewGroup("Contratos");
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private GetMyMonthQueryHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new(used.Entries, used.Closings, used.Groups, used.Payments, used.Housing, used.Tenant, used.Clock);
    }

    private Task<Result<MyMonthView>> Get(string month = "2026-10", Guid? user = null) =>
        Handler().Handle(new GetMyMonthQuery(user ?? Resident, month), default);

    private void OpenOctober(decimal amount = 1000m)
    {
        Expense expense = _kit.NewFixed(_contracts, M(2026, 10), amount, "Portaria", supplier: "Segurança Ltda");
        _kit.NewEntry(expense, M(2026, 10));
    }

    private (UnitCharge First, UnitCharge Second) CloseOctober(DateOnly? dueDate = null)
    {
        OpenOctober();
        MonthClosing closing = _kit.CloseMonth(M(2026, 10), dueDate);
        List<UnitCharge> charges = [.. closing.Charges.OrderBy(charge => charge.Position)];

        return (charges[0], charges[1]);
    }

    private Payment Pay(UnitCharge charge, decimal amount, DateOnly? receivedOn = null) =>
        _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, charge.Id, M(2026, 10), amount, receivedOn ?? new DateOnly(2026, 11, 5), PaymentMethod.Pix, null, Manager, FinanceTestKit.Now, Today).Value);

    // ---- Pedido ----

    [Fact]
    public async Task WithoutACondominium_Fails_AndNobodyIsLookedUp()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<MyMonthView> result = await Handler(kit).Handle(new GetMyMonthQuery(Resident, "2026-10"), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Housing.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("2026-13")]
    [InlineData("2026/10")]
    public async Task AnInvalidMonth_Fails(string? month)
    {
        _kit.Housing.Lives(Resident, Unit101);

        Result<MyMonthView> result = await Handler().Handle(new GetMyMonthQuery(Resident, month!), default);

        result.Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task TheHousingIsAskedAboutTheAccountOfTheToken_AsOfTodayInBrasilia()
    {
        CloseOctober();
        _kit.Housing.Lives(Resident, Unit101);

        await Get();

        _kit.Housing.AskedAbout.Should().Be(Resident);
        _kit.Housing.AskedOn.Should().Be(Today);
        _kit.Housing.Calls.Should().Be(1);
    }

    // ---- Quem não tem unidade ----

    [Fact]
    public async Task SomeoneWithoutAnyLink_SeesNothingFinancial_EvenOfAClosedMonth()
    {
        CloseOctober();

        MyMonthView view = (await Get()).Value;

        view.Access.Should().Be(MyMonthAccess.None);
        view.Closed.Should().BeFalse();
        view.Units.Should().BeEmpty();
        view.Expenses.Should().BeNull();
        view.Month.Should().Be("2026-10");
        view.AsOf.Should().Be(Today);
    }

    [Fact]
    public async Task ADependent_OnlyGetsTheNoticeAndNothingFinancial()
    {
        CloseOctober();
        _kit.Housing.LivesAsDependent(Resident);

        MyMonthView view = (await Get()).Value;

        view.Access.Should().Be(MyMonthAccess.Dependent);
        view.Closed.Should().BeFalse();
        view.Units.Should().BeEmpty();
        view.Expenses.Should().BeNull();
    }

    [Fact]
    public async Task ADependentWhoIsAlsoTheOwnerOfAnotherUnit_SeesTheUnit()
    {
        CloseOctober();
        _kit.Housing.LivesAsDependent(Resident, Unit102);

        MyMonthView view = (await Get()).Value;

        view.Access.Should().Be(MyMonthAccess.Unit);
        view.Units.Select(unit => unit.Number).Should().Equal("102");
    }

    // ---- A cobrança da unidade ----

    [Fact]
    public async Task AClosedMonth_ShowsTheChargeOfTheUnitWithNothingPaid()
    {
        (UnitCharge first, _) = CloseOctober(new DateOnly(2026, 11, 10));
        _kit.Housing.Lives(Resident, Unit101);

        MyMonthView view = (await Get()).Value;

        view.Access.Should().Be(MyMonthAccess.Unit);
        view.Closed.Should().BeTrue();
        MyUnitView unit = view.Units.Should().ContainSingle().Subject;
        (unit.UnitId, unit.Block, unit.Number).Should().Be((Unit101.UnitId, null, "101"));
        unit.Charge.Should().Be(new MyChargeView(first.Id, new DateOnly(2026, 11, 10), 600m, 0m, 600m, ChargeStatus.Open, null));
    }

    [Fact]
    public async Task TheResident_NeverSeesTheChargeOfAnotherUnit()
    {
        CloseOctober();
        _kit.Housing.Lives(Resident, Unit102);

        MyMonthView view = (await Get()).Value;

        MyUnitView unit = view.Units.Should().ContainSingle().Subject;
        (unit.Block, unit.Number).Should().Be(("A", "102"));
        unit.Charge!.Amount.Should().Be(400m);
    }

    [Fact]
    public async Task ThePaymentsOfOtherUnits_DoNotChangeTheChargeOfTheResident()
    {
        (UnitCharge first, UnitCharge second) = CloseOctober();
        Pay(second, 400m);
        _kit.Housing.Lives(Resident, Unit101);

        MyChargeView charge = (await Get()).Value.Units.Single().Charge!;

        charge.ChargeId.Should().Be(first.Id);
        (charge.Paid, charge.Balance, charge.Status, charge.LastReceivedOn).Should().Be((0m, 600m, ChargeStatus.Open, null));
    }

    [Fact]
    public async Task APartialPayment_ShowsWhatWasPaidTheBalanceAndTheLastDay()
    {
        (UnitCharge first, _) = CloseOctober(new DateOnly(2026, 11, 10));
        Pay(first, 200m, new DateOnly(2026, 11, 3));
        Pay(first, 100m, new DateOnly(2026, 11, 5));
        _kit.Housing.Lives(Resident, Unit101);

        MyChargeView charge = (await Get()).Value.Units.Single().Charge!;

        (charge.Paid, charge.Balance, charge.Status).Should().Be((300m, 300m, ChargeStatus.Partial));
        charge.LastReceivedOn.Should().Be(new DateOnly(2026, 11, 5));
    }

    [Fact]
    public async Task AFullyPaidCharge_IsPaidWithNoBalance()
    {
        (UnitCharge first, _) = CloseOctober(new DateOnly(2026, 11, 10));
        Pay(first, 600m);
        _kit.Housing.Lives(Resident, Unit101);

        MyChargeView charge = (await Get()).Value.Units.Single().Charge!;

        (charge.Paid, charge.Balance, charge.Status).Should().Be((600m, 0m, ChargeStatus.Paid));
    }

    [Fact]
    public async Task AfterTheDueDate_WhatIsNotPaidIsOverdue_PartialIncluded()
    {
        (UnitCharge first, _) = CloseOctober(new DateOnly(2026, 11, 1));
        Pay(first, 150m);
        _kit.Housing.Lives(Resident, Unit101, Unit102);

        List<MyUnitView> units = [.. (await Get()).Value.Units];

        units.Select(unit => unit.Charge!.Status).Should().Equal(ChargeStatus.Overdue, ChargeStatus.Overdue);
        units.Select(unit => unit.Charge!.Balance).Should().Equal(450m, 400m);
    }

    [Fact]
    public async Task OnTheDueDateItself_TheChargeIsStillOpen()
    {
        CloseOctober(Today);
        _kit.Housing.Lives(Resident, Unit101);

        (await Get()).Value.Units.Single().Charge!.Status.Should().Be(ChargeStatus.Open);
    }

    [Fact]
    public async Task AReversedPayment_NoLongerCounts()
    {
        (UnitCharge first, _) = CloseOctober(new DateOnly(2026, 11, 1));
        Pay(first, 600m).Reverse("Unidade errada", Manager, FinanceTestKit.Now);
        _kit.Housing.Lives(Resident, Unit101);

        MyChargeView charge = (await Get()).Value.Units.Single().Charge!;

        (charge.Paid, charge.Balance, charge.Status, charge.LastReceivedOn).Should().Be((0m, 600m, ChargeStatus.Overdue, null));
    }

    [Fact]
    public async Task AResidentWithTwoUnits_GetsOneChargeForEach_InTheOrderTheHousingGaveThem()
    {
        CloseOctober();
        _kit.Housing.Lives(Resident, Unit102, Unit101);

        MyMonthView view = (await Get()).Value;

        view.Units.Select(unit => (unit.Number, unit.Charge!.Amount)).Should().Equal(("102", 400m), ("101", 600m));
    }

    [Fact]
    public async Task AUnitThatReceivedNoChargeInTheMonth_ComesWithoutOne()
    {
        CloseOctober();
        ResidentUnit newcomer = new(Guid.CreateVersion7(), "B", "201");
        _kit.Housing.Lives(Resident, Unit101, newcomer);

        MyMonthView view = (await Get()).Value;

        view.Closed.Should().BeTrue();
        view.Units.Select(unit => (unit.Number, unit.Charge is null)).Should().Equal(("101", false), ("201", true));
    }

    [Fact]
    public async Task TheCharges_AreOnlyThoseOfTheRequestedMonth()
    {
        CloseOctober(new DateOnly(2026, 11, 1));
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 9), 500m, "Antiga"), M(2026, 9));
        _kit.CloseMonth(M(2026, 9), new DateOnly(2026, 10, 10));
        _kit.Housing.Lives(Resident, Unit101);

        MyChargeView october = (await Get()).Value.Units.Single().Charge!;
        MyChargeView september = (await Get("2026-09")).Value.Units.Single().Charge!;

        october.Amount.Should().Be(600m);
        september.Amount.Should().Be(300m);
        september.DueDate.Should().Be(new DateOnly(2026, 10, 10));
    }

    // ---- Mês aberto ----

    [Fact]
    public async Task AnOpenMonth_HasNoChargeAndHidesTheExpenses_BecauseTheValuesStillChange()
    {
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 11), 700m, "Portaria"), M(2026, 11));
        _kit.Housing.Lives(Resident, Unit101);

        MyMonthView view = (await Get("2026-11")).Value;

        view.Access.Should().Be(MyMonthAccess.Unit);
        view.Closed.Should().BeFalse();
        view.Units.Should().ContainSingle().Which.Charge.Should().BeNull();
        view.Expenses.Should().BeNull();
    }

    [Fact]
    public async Task AMonthThatWasNeverOpened_IsTheSameAsAnOpenOne()
    {
        _kit.Housing.Lives(Resident, Unit101, Unit102);

        MyMonthView view = (await Get("2026-12")).Value;

        view.Closed.Should().BeFalse();
        view.Units.Select(unit => unit.Charge).Should().OnlyContain(charge => charge == null);
        view.Expenses.Should().BeNull();
    }

    // ---- As despesas do mês ----

    [Fact]
    public async Task AClosedMonth_ShowsTheExpensesByGroup_AlphabeticallyWithTheTotal()
    {
        ExpenseGroup works = _kit.NewGroup("Obras");
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 10), 700m, "Portaria"), M(2026, 10));
        _kit.NewEntry(_kit.NewFixed(works, M(2026, 10), 300m, "Pintura"), M(2026, 10));
        _kit.NewEntry(_kit.NewFixed(_contracts, M(2026, 10), 50m, "Limpeza"), M(2026, 10));
        _kit.CloseMonth(M(2026, 10));
        _kit.Housing.Lives(Resident, Unit101);

        MyExpensesView expenses = (await Get()).Value.Expenses!;

        expenses.Total.Should().Be(1050m);
        expenses.Groups.Select(group => (group.GroupName, group.Total)).Should().Equal(("Contratos", 750m), ("Obras", 300m));
        expenses.Groups.Should().OnlyContain(group => group.GroupId != Guid.Empty);
    }

    [Fact]
    public async Task TheExpenses_LeaveCanceledOnesOutAndGroupsThatEndedUpWithNothing()
    {
        CloseOctober();
        ExpenseGroup canceled = _kit.NewGroup("Cancelados");
        _kit.NewEntry(_kit.NewFixed(canceled, M(2026, 10), 100m, "Obra cancelada"), M(2026, 10)).SetStatus(EntryStatus.Canceled);
        _kit.Housing.Lives(Resident, Unit101);

        MyExpensesView expenses = (await Get()).Value.Expenses!;

        expenses.Total.Should().Be(1000m);
        expenses.Groups.Select(group => group.GroupName).Should().Equal("Contratos");
    }

    [Fact]
    public void TheExpenses_ShowNeitherTheSupplierNorTheNoteNorWhatIsOverdue()
    {
        string[] hidden = ["Supplier", "Note", "NeedsAmount", "Overdue", "Entries", "Description"];

        string[] shown = [.. typeof(MyExpensesView).GetProperties().Concat(typeof(MyExpenseGroupView).GetProperties()).Select(property => property.Name)];

        shown.Should().NotContain(hidden, "fornecedor, observação e atraso de despesa são do síndico");
    }

    [Fact]
    public void TheView_ShowsNothingOfTheCollectionOfTheCondominium()
    {
        string[] hidden = ["Charged", "Received", "Units", "Open", "Partial", "Overdue", "OverdueAmount", "Delinquency"];

        string[] shown =
        [
            .. typeof(MyMonthView).GetProperties().Select(property => property.Name),
            .. typeof(MyUnitView).GetProperties().Select(property => property.Name),
            .. typeof(MyChargeView).GetProperties().Select(property => property.Name),
        ];

        shown.Where(name => name != "Units").Should().NotIntersectWith(hidden, "o que o prédio recebeu e quem está atrasado são do síndico");
    }
}
