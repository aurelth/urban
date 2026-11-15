using FluentAssertions;
using Urban.Finance.Application.Expenses;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class DeleteExpenseCommandHandlerTests
{
    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _group;

    public DeleteExpenseCommandHandlerTests()
    {
        _group = _kit.NewGroup();
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private DeleteExpenseCommandHandler Handler() => new(_kit.Expenses, _kit.Entries, _kit.Closings, _kit.Tenant, _kit.Atomic);

    [Fact]
    public async Task Delete_RemovesTheExpenseAndAllItsEntries_AndLeavesTheOthersAlone()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        Expense other = _kit.NewFixed(_group, M(2026, 9), description: "Jardinagem");
        _kit.NewEntry(expense, M(2026, 9));
        _kit.NewEntry(expense, M(2026, 11));
        _kit.NewEntry(expense, M(2027, 1));
        ExpenseEntry othersEntry = _kit.NewEntry(other, M(2026, 11));

        Result result = await Handler().Handle(new DeleteExpenseCommand(expense.Id, "5"), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Expenses.All.Should().BeEquivalentTo([other]);
        _kit.Entries.All.Should().BeEquivalentTo([othersEntry]);
        _kit.Entries.Removed.Should().HaveCount(3);
        _kit.Atomic.Runs.Should().Be(1);
    }

    [Fact]
    public async Task Delete_WorksForAnExpenseWithoutEntries()
    {
        Expense expense = _kit.NewInstallment(_group, M(2026, 12));

        Result result = await Handler().Handle(new DeleteExpenseCommand(expense.Id, "5"), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Expenses.All.Should().BeEmpty();
        _kit.Entries.Removed.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_WorksForEveryKind()
    {
        Expense single = _kit.NewSingle(_group, M(2026, 11));
        Expense variable = _kit.NewVariable(_group, M(2026, 11));
        _kit.NewEntry(single, M(2026, 11));
        _kit.NewEntry(variable, M(2026, 11));

        (await Handler().Handle(new DeleteExpenseCommand(single.Id, "5"), default)).IsSuccess.Should().BeTrue();
        (await Handler().Handle(new DeleteExpenseCommand(variable.Id, "5"), default)).IsSuccess.Should().BeTrue();

        _kit.Expenses.All.Should().BeEmpty();
        _kit.Entries.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_RemovesTheCanceledAndPendingEntries()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        _kit.NewEntry(expense, M(2026, 11)).SetStatus(EntryStatus.Canceled).IsSuccess.Should().BeTrue();
        _kit.NewEntry(expense, M(2026, 12));

        Result result = await Handler().Handle(new DeleteExpenseCommand(expense.Id, "5"), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Entries.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_RefusesWhenAnEntryIsInAClosedMonth_AndRemovesNothing()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        _kit.NewEntry(expense, M(2026, 10));
        _kit.NewEntry(expense, M(2026, 11));
        _kit.CloseMonth(M(2026, 10));

        Result result = await Handler().Handle(new DeleteExpenseCommand(expense.Id, "5"), default);

        result.Error.Should().Be(ExpenseErrors.DeleteHasClosedMonths);
        _kit.Expenses.All.Should().HaveCount(1);
        _kit.Entries.All.Should().HaveCount(2);
        _kit.Entries.Removed.Should().BeEmpty();
        _kit.Expenses.Removed.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_RefusesWhenAnEntryIsPaid_AndRemovesNothing()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        _kit.NewEntry(expense, M(2026, 11)).SetStatus(EntryStatus.Paid).IsSuccess.Should().BeTrue();
        _kit.NewEntry(expense, M(2026, 12));

        Result result = await Handler().Handle(new DeleteExpenseCommand(expense.Id, "5"), default);

        result.Error.Should().Be(ExpenseErrors.DeleteHasPaidEntries);
        _kit.Expenses.All.Should().HaveCount(1);
        _kit.Entries.All.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_ChecksTheClosedMonthBeforeThePaidOne()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        _kit.NewEntry(expense, M(2026, 10)).SetStatus(EntryStatus.Paid).IsSuccess.Should().BeTrue();
        _kit.CloseMonth(M(2026, 10));

        (await Handler().Handle(new DeleteExpenseCommand(expense.Id, "5"), default)).Error.Should().Be(ExpenseErrors.DeleteHasClosedMonths);
    }

    [Fact]
    public async Task Delete_Failures()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));

        (await Handler().Handle(new DeleteExpenseCommand(expense.Id, null), default)).Error.Should().Be(FinanceErrors.VersionRequired);
        (await Handler().Handle(new DeleteExpenseCommand(expense.Id, "4"), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await Handler().Handle(new DeleteExpenseCommand(Guid.CreateVersion7(), "5"), default)).Error.Should().Be(ExpenseErrors.NotFound);
        _kit.Expenses.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task Delete_WithoutATenant_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();
        DeleteExpenseCommandHandler handler = new(kit.Expenses, kit.Entries, kit.Closings, kit.Tenant, kit.Atomic);

        (await handler.Handle(new DeleteExpenseCommand(Guid.CreateVersion7(), "5"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Delete_WhenTheStoreRefuses_ReturnsTheFailure()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9));
        _kit.Expenses.RemoveFailure = FinanceErrors.ConcurrencyConflict;

        Result result = await Handler().Handle(new DeleteExpenseCommand(expense.Id, "5"), default);

        result.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
    }
}
