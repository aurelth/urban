using FluentAssertions;
using Urban.Finance.Application.Expenses;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class ExpenseChangesHandlersTests
{
    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _group;

    public ExpenseChangesHandlersTests()
    {
        _group = _kit.NewGroup();
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private UpdateExpenseCommandHandler UpdateHandler() => new(_kit.Expenses, _kit.Groups, _kit.Tenant, _kit.Clock);

    private ChangeExpenseAmountCommandHandler ChangeHandler() => new(_kit.Expenses, _kit.Entries, _kit.Closings, _kit.Tenant, _kit.Atomic, _kit.Clock);

    private SetInstallmentAmountCommandHandler InstallmentHandler() => new(_kit.Expenses, _kit.Entries, _kit.Closings, _kit.Tenant, _kit.Atomic, _kit.Clock);

    private EndExpenseCommandHandler EndHandler() => new(_kit.Expenses, _kit.Tenant, _kit.Clock);

    // ---- update ----

    [Fact]
    public async Task Update_ChangesDescriptionSupplierAndGroup()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        ExpenseGroup other = _kit.NewGroup("Manutenção das garagens");

        Result<ExpenseView> result = await UpdateHandler().Handle(new UpdateExpenseCommand(expense.Id, "5", " Zeladoria e limpeza ", "Limpa Tudo", other.Id, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Zeladoria e limpeza");
        result.Value.Supplier.Should().Be("Limpa Tudo");
        result.Value.GroupId.Should().Be(other.Id);
        result.Value.Version.Should().Be("6");
        _kit.Expenses.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_WithoutTheVersion_OrWithAnOldOne_Fails()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));

        (await UpdateHandler().Handle(new UpdateExpenseCommand(expense.Id, null, "x", null, _group.Id, 10), default))
            .Error.Should().Be(FinanceErrors.VersionRequired);
        (await UpdateHandler().Handle(new UpdateExpenseCommand(expense.Id, "4", "x", null, _group.Id, 10), default))
            .Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        expense.Description.Should().Be("Zeladoria");
        _kit.Expenses.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_OfAnUnknownExpense_OrToAnUnknownGroup_Fails()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));

        (await UpdateHandler().Handle(new UpdateExpenseCommand(Guid.CreateVersion7(), "5", "x", null, _group.Id, 10), default))
            .Error.Should().Be(ExpenseErrors.NotFound);
        (await UpdateHandler().Handle(new UpdateExpenseCommand(expense.Id, "5", "x", null, Guid.CreateVersion7(), 10), default))
            .Error.Should().Be(ExpenseGroupErrors.NotFound);
    }

    [Fact]
    public async Task Update_WithAnInvalidDescription_Fails()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));

        (await UpdateHandler().Handle(new UpdateExpenseCommand(expense.Id, "5", "", null, _group.Id, 10), default))
            .Error.Should().Be(ExpenseErrors.DescriptionInvalid);
    }

    [Fact]
    public async Task Update_WithoutATenant_Fails()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        _kit.WithoutATenant();

        (await UpdateHandler().Handle(new UpdateExpenseCommand(expense.Id, "5", "x", null, _group.Id, 10), default))
            .Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- change the amount of a fixed expense ----

    [Fact]
    public async Task ChangeAmount_AppliesFromTheMonth_AndKeepsTheHistory()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9), 300m);

        Result<ExpenseView> result = await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, "5", "2027-01", 330m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("6");
        result.Value.AmountChanges.Should().BeEquivalentTo([new AmountChangeView("2026-09", 300m), new AmountChangeView("2027-01", 330m)]);
        expense.AmountFor(M(2026, 12)).Should().Be(300m);
        expense.AmountFor(M(2027, 1)).Should().Be(330m);
    }

    [Fact]
    public async Task ChangeAmount_UpdatesTheEntriesFromThatMonthThatWereNeverEdited()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9), 300m);
        ExpenseEntry september = _kit.NewEntry(expense, M(2026, 9));
        ExpenseEntry november = _kit.NewEntry(expense, M(2026, 11));
        ExpenseEntry december = _kit.NewEntry(expense, M(2026, 12));
        december.SetAmount(305m);

        await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, "5", "2026-11", 330m), default);

        september.Amount.Should().Be(300m);
        november.Amount.Should().Be(330m);
        december.Amount.Should().Be(305m);
        _kit.Atomic.Runs.Should().Be(1);
    }

    [Fact]
    public async Task ChangeAmount_Failures()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9), 300m);
        Expense single = _kit.NewSingle(_group, M(2026, 9));

        (await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, null, "2027-01", 330m), default))
            .Error.Should().Be(FinanceErrors.VersionRequired);
        (await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, "4", "2027-01", 330m), default))
            .Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await ChangeHandler().Handle(new ChangeExpenseAmountCommand(Guid.CreateVersion7(), "5", "2027-01", 330m), default))
            .Error.Should().Be(ExpenseErrors.NotFound);
        (await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, "5", "janeiro", 330m), default))
            .Error.Should().Be(MonthErrors.Invalid);
        (await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, "5", "2026-08", 330m), default))
            .Error.Should().Be(ExpenseErrors.ChangeBeforeStart);
        (await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, "5", "2027-01", 0m), default))
            .Error.Should().Be(ExpenseErrors.AmountInvalid);
        (await ChangeHandler().Handle(new ChangeExpenseAmountCommand(single.Id, "5", "2026-09", 10m), default))
            .Error.Should().Be(ExpenseErrors.NotFixed);
        _kit.Expenses.Saved.Should().BeEmpty();
    }

    // ---- change one parcel ----

    [Fact]
    public async Task SetInstallmentAmount_ChangesOnlyThatParcel_AndItsEntryWhenNeverEdited()
    {
        Expense expense = _kit.NewInstallment(_group, M(2026, 9), total: 600m, count: 6, current: 1);
        ExpenseEntry october = _kit.NewEntry(expense, M(2026, 10));
        ExpenseEntry november = _kit.NewEntry(expense, M(2026, 11));
        november.SetAmount(99m);

        Result<ExpenseView> result = await InstallmentHandler().Handle(new SetInstallmentAmountCommand(expense.Id, "5", 2, 120.40m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Installments.Single(parcel => parcel.Number == 2).Amount.Should().Be(120.40m);
        result.Value.Installments.Single(parcel => parcel.Number == 3).Amount.Should().Be(100m);
        october.Amount.Should().Be(120.40m);
        november.Amount.Should().Be(99m);
    }

    [Fact]
    public async Task SetInstallmentAmount_Failures()
    {
        Expense expense = _kit.NewInstallment(_group, M(2026, 9));
        Expense fixedExpense = _kit.NewFixed(_group, M(2026, 9));

        (await InstallmentHandler().Handle(new SetInstallmentAmountCommand(expense.Id, null, 2, 10m), default))
            .Error.Should().Be(FinanceErrors.VersionRequired);
        (await InstallmentHandler().Handle(new SetInstallmentAmountCommand(expense.Id, "4", 2, 10m), default))
            .Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await InstallmentHandler().Handle(new SetInstallmentAmountCommand(Guid.CreateVersion7(), "5", 2, 10m), default))
            .Error.Should().Be(ExpenseErrors.NotFound);
        (await InstallmentHandler().Handle(new SetInstallmentAmountCommand(expense.Id, "5", 9, 10m), default))
            .Error.Should().Be(ExpenseErrors.InstallmentNotFound);
        (await InstallmentHandler().Handle(new SetInstallmentAmountCommand(expense.Id, "5", 2, 0m), default))
            .Error.Should().Be(ExpenseErrors.AmountInvalid);
        (await InstallmentHandler().Handle(new SetInstallmentAmountCommand(fixedExpense.Id, "5", 1, 10m), default))
            .Error.Should().Be(ExpenseErrors.NotInstallment);
    }

    // ---- end ----

    [Fact]
    public async Task End_StopsTheScheduleAfterTheMonth()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));

        Result<ExpenseView> result = await EndHandler().Handle(new EndExpenseCommand(expense.Id, "5", "2027-02"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.EndMonth.Should().Be("2027-02");
        result.Value.Version.Should().Be("6");
        expense.IsDueIn(M(2027, 3)).Should().BeFalse();
    }

    [Fact]
    public async Task End_Failures()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        Expense single = _kit.NewSingle(_group, M(2026, 9));

        (await EndHandler().Handle(new EndExpenseCommand(expense.Id, null, "2027-02"), default)).Error.Should().Be(FinanceErrors.VersionRequired);
        (await EndHandler().Handle(new EndExpenseCommand(expense.Id, "4", "2027-02"), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await EndHandler().Handle(new EndExpenseCommand(Guid.CreateVersion7(), "5", "2027-02"), default)).Error.Should().Be(ExpenseErrors.NotFound);
        (await EndHandler().Handle(new EndExpenseCommand(expense.Id, "5", "fevereiro"), default)).Error.Should().Be(MonthErrors.Invalid);
        (await EndHandler().Handle(new EndExpenseCommand(expense.Id, "5", "2026-08"), default)).Error.Should().Be(ExpenseErrors.EndBeforeStart);
        (await EndHandler().Handle(new EndExpenseCommand(single.Id, "5", "2026-09"), default)).Error.Should().Be(ExpenseErrors.EndNotAllowed);
        _kit.Expenses.Saved.Should().BeEmpty();
    }

    // ---- closed months ----

    [Fact]
    public async Task ChangeAmount_DoesNotTouchTheEntriesOfAClosedMonth()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9), 300m);
        ExpenseEntry november = _kit.NewEntry(expense, M(2026, 11));
        ExpenseEntry december = _kit.NewEntry(expense, M(2026, 12));
        _kit.CloseMonth(M(2026, 11));

        Result<ExpenseView> result = await ChangeHandler().Handle(new ChangeExpenseAmountCommand(expense.Id, "5", "2026-11", 330m), default);

        result.IsSuccess.Should().BeTrue();
        november.Amount.Should().Be(300m);
        december.Amount.Should().Be(330m);
    }

    [Fact]
    public async Task SetInstallmentAmount_DoesNotTouchTheEntryOfAClosedMonth()
    {
        Expense expense = _kit.NewInstallment(_group, M(2026, 9), total: 600m, count: 6, current: 1);
        ExpenseEntry november = _kit.NewEntry(expense, M(2026, 11));
        _kit.CloseMonth(M(2026, 11));

        Result<ExpenseView> result = await InstallmentHandler().Handle(new SetInstallmentAmountCommand(expense.Id, "5", 3, 120.40m), default);

        result.IsSuccess.Should().BeTrue();
        november.Amount.Should().Be(100m);
    }
}
