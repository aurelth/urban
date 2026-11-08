using FluentAssertions;
using Urban.Finance.Application.Expenses;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class CreateExpenseCommandHandlerTests
{
    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _group;

    public CreateExpenseCommandHandlerTests()
    {
        _group = _kit.NewGroup();
    }

    private CreateExpenseCommandHandler Handler() => new(_kit.Expenses, _kit.Groups, _kit.Entries, _kit.Closings, _kit.Tenant, _kit.Atomic, _kit.Clock);

    private CreateExpenseCommand Single(string? month = "2026-11", decimal? amount = 250m) =>
        new(ExpenseKind.Single, _group.Id, "Troca da lâmpada", "Eletro Silva", month, null, amount, null, null, 10);

    private CreateExpenseCommand Fixed(string? start = "2026-09", string? end = null, decimal? amount = 300m) =>
        new(ExpenseKind.Fixed, _group.Id, "Zeladoria", null, start, end, amount, null, null, 10);

    private CreateExpenseCommand Variable(string? start = "2026-09") =>
        new(ExpenseKind.Variable, _group.Id, "Energia elétrica", "Enel", start, null, null, null, null, 10);

    private CreateExpenseCommand Installment(string? start = "2026-09", decimal? total = 600m, int? count = 6, int? current = 4) =>
        new(ExpenseKind.Installment, _group.Id, "Obra da garagem", "Benedett", start, null, total, count, current, 10);

    // ---- one-off ----

    [Fact]
    public async Task Handle_Single_StoresTheExpenseAndTheEntryOfItsMonth()
    {
        Result<ExpenseView> result = await Handler().Handle(Single(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(ExpenseKind.Single);
        result.Value.Description.Should().Be("Troca da lâmpada");
        result.Value.Supplier.Should().Be("Eletro Silva");
        result.Value.GroupId.Should().Be(_group.Id);
        result.Value.StartMonth.Should().Be("2026-11");
        result.Value.CurrentAmount.Should().Be(250m);
        result.Value.Version.Should().Be("1");

        _kit.Expenses.All.Should().ContainSingle().Which.CondominiumId.Should().Be(_kit.CondominiumId);
        ExpenseEntry entry = _kit.Entries.Added.Should().ContainSingle().Subject;
        entry.Month.Should().Be(FinanceTestKit.M(2026, 11));
        entry.Amount.Should().Be(250m);
        _kit.Atomic.Runs.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Single_ForTheMonthAfterTheCurrentOne_AlsoGeneratesTheEntry()
    {
        await Handler().Handle(Single(month: "2026-12"), default);

        _kit.Entries.Added.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_Single_FurtherThanFiveYearsAhead_StoresOnlyTheExpense_AndTheEntryComesWhenTheMonthOpens()
    {
        Result<ExpenseView> result = await Handler().Handle(Single(month: "2032-01"), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Expenses.All.Should().HaveCount(1);
        _kit.Entries.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Single_WithinFiveYears_GeneratesTheEntryWithTheDueDateOfThatMonth()
    {
        Result<ExpenseView> result = await Handler().Handle(Single(month: "2027-01"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.DueDay.Should().Be(10);
        _kit.Entries.Added.Should().ContainSingle().Which.DueDate.Should().Be(new DateOnly(2027, 1, 10));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(32)]
    public async Task Handle_WithoutAValidDueDay_Fails_AndStoresNothing(int? dueDay)
    {
        Result<ExpenseView> result = await Handler().Handle(Single(month: "2026-11") with { DueDay = dueDay }, default);

        result.Error.Should().Be(ExpenseErrors.DueDayInvalid);
        _kit.Expenses.All.Should().BeEmpty();
        _kit.Entries.Added.Should().BeEmpty();
    }

    // ---- fixed, variable, installment ----

    [Fact]
    public async Task Handle_Fixed_GeneratesOnlyTheEntryOfTheStartMonth()
    {
        Result<ExpenseView> result = await Handler().Handle(Fixed(start: "2026-09"), default);

        result.Value.Kind.Should().Be(ExpenseKind.Fixed);
        result.Value.AmountChanges.Should().ContainSingle().Which.Should().Be(new AmountChangeView("2026-09", 300m));
        _kit.Entries.Added.Should().ContainSingle().Which.Month.Should().Be(FinanceTestKit.M(2026, 9));
    }

    [Fact]
    public async Task Handle_Fixed_WithAnEndMonth_KeepsIt()
    {
        Result<ExpenseView> result = await Handler().Handle(Fixed(end: "2027-02"), default);

        result.Value.EndMonth.Should().Be("2027-02");
        result.Value.LastMonth.Should().Be("2027-02");
    }

    [Fact]
    public async Task Handle_Variable_StartsPendingWithoutAnAmount()
    {
        Result<ExpenseView> result = await Handler().Handle(Variable(), default);

        result.Value.Kind.Should().Be(ExpenseKind.Variable);
        result.Value.CurrentAmount.Should().BeNull();
        ExpenseEntry entry = _kit.Entries.Added.Should().ContainSingle().Subject;
        entry.NeedsAmount.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Installment_LabelsTheCurrentParcelAndExposesTheSchedule()
    {
        Result<ExpenseView> result = await Handler().Handle(Installment(start: "2026-09", total: 600m, count: 6, current: 4), default);

        result.Value.Kind.Should().Be(ExpenseKind.Installment);
        result.Value.InstallmentCount.Should().Be(6);
        result.Value.CurrentInstallment.Should().Be(4);
        result.Value.LastMonth.Should().Be("2026-11");
        result.Value.Installments.Should().HaveCount(6);
        result.Value.Installments.Single(parcel => parcel.Number == 4).Should().Be(new InstallmentView(4, "2026-09", 100m));
        result.Value.Installments.Single(parcel => parcel.Number == 6).Month.Should().Be("2026-11");
        result.Value.Installments.Single(parcel => parcel.Number == 1).Month.Should().Be("2026-06");

        ExpenseEntry entry = _kit.Entries.Added.Should().ContainSingle().Subject;
        entry.Label.Should().Be("4/6");
        entry.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_Installment_WithoutTheCurrentParcel_StartsAtTheFirst()
    {
        Result<ExpenseView> result = await Handler().Handle(Installment(current: null), default);

        result.Value.CurrentInstallment.Should().Be(1);
    }

    // ---- failures ----

    [Fact]
    public async Task Handle_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await Handler().Handle(Single(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Handle_WithAGroupThatDoesNotExist_Fails()
    {
        CreateExpenseCommand command = Single() with { GroupId = Guid.CreateVersion7() };

        (await Handler().Handle(command, default)).Error.Should().Be(ExpenseGroupErrors.NotFound);
        _kit.Expenses.All.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("09/2026")]
    [InlineData("2026-13")]
    public async Task Handle_WithAnInvalidStartMonth_Fails(string? month)
    {
        (await Handler().Handle(Single(month: month), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task Handle_WithAnInvalidEndMonth_Fails()
    {
        (await Handler().Handle(Fixed(end: "janeiro"), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task Handle_WithTheEndBeforeTheStart_Fails()
    {
        (await Handler().Handle(Fixed(start: "2026-09", end: "2026-08"), default)).Error.Should().Be(ExpenseErrors.EndBeforeStart);
        _kit.Expenses.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutOrWithAnInvalidAmount_Fails()
    {
        (await Handler().Handle(Single(amount: null), default)).Error.Should().Be(ExpenseErrors.AmountInvalid);
        (await Handler().Handle(Fixed(amount: 0m), default)).Error.Should().Be(ExpenseErrors.AmountInvalid);
        (await Handler().Handle(Installment(total: null), default)).Error.Should().Be(ExpenseErrors.AmountInvalid);
    }

    [Fact]
    public async Task Handle_InstallmentWithAnInvalidCountOrCurrentParcel_Fails()
    {
        (await Handler().Handle(Installment(count: null), default)).Error.Should().Be(ExpenseErrors.InstallmentCountInvalid);
        (await Handler().Handle(Installment(count: 1), default)).Error.Should().Be(ExpenseErrors.InstallmentCountInvalid);
        (await Handler().Handle(Installment(count: 6, current: 7), default)).Error.Should().Be(ExpenseErrors.CurrentInstallmentInvalid);
    }

    [Fact]
    public async Task Handle_WithAnInvalidDescription_FailsAndStoresNothing()
    {
        CreateExpenseCommand command = Single() with { Description = "  " };

        (await Handler().Handle(command, default)).Error.Should().Be(ExpenseErrors.DescriptionInvalid);
        _kit.Expenses.All.Should().BeEmpty();
        _kit.Entries.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAnUnknownKind_Fails()
    {
        CreateExpenseCommand command = Single() with { Kind = (ExpenseKind)0 };

        (await Handler().Handle(command, default)).Error.Should().Be(ExpenseErrors.KindInvalid);
    }

    [Fact]
    public async Task Handle_WhenTheEntryCannotBeStored_FailsAndTheAtomicWorkSeesTheFailure()
    {
        _kit.Entries.AddRangeFailure = FinanceErrors.ConcurrencyConflict;

        Result<ExpenseView> result = await Handler().Handle(Single(), default);

        result.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        _kit.Atomic.Outcome!.IsFailure.Should().BeTrue();
    }

    // ---- closed months ----

    [Fact]
    public async Task Handle_Single_ForAClosedMonth_StoresTheExpenseButDoesNotAddAnEntryToTheClosedMonth()
    {
        _kit.NewEntry(_kit.NewFixed(_group, FinanceTestKit.M(2026, 9), 600m, "Zeladoria"), FinanceTestKit.M(2026, 11));
        _kit.CloseMonth(FinanceTestKit.M(2026, 11));
        _kit.Entries.Added.Clear();

        Result<ExpenseView> result = await Handler().Handle(Single(), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Expenses.All.Should().HaveCount(2);
        _kit.Entries.Added.Should().BeEmpty();
    }
}
