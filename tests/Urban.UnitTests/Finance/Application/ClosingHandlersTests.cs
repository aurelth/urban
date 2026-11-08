using FluentAssertions;
using Urban.Finance.Application.Months;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>Fechar e reabrir um mês, e o que um mês fechado deixa de aceitar.</summary>
public class ClosingHandlersTests
{
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateOnly DueDate = new(2026, 12, 10);

    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _group;

    public ClosingHandlersTests()
    {
        _group = _kit.NewGroup();
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private CloseMonthCommandHandler CloseHandler() => new(_kit.Entries, _kit.Closings, _kit.Groups, _kit.Basis, _kit.Tenant, _kit.Atomic, _kit.Clock);

    private ReopenMonthCommandHandler ReopenHandler() => new(_kit.Entries, _kit.Closings, _kit.Groups, _kit.Tenant, _kit.Atomic, _kit.Clock);

    private ExpenseEntry NovemberEntry(decimal amount, string description = "Zeladoria")
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9), amount, description);

        return _kit.NewEntry(expense, M(2026, 11));
    }

    // ---- close ----

    [Fact]
    public async Task Close_SplitsTheTotalAndReturnsTheMonthWithItsCharges()
    {
        NovemberEntry(600m);
        NovemberEntry(400m, "Limpeza");

        Result<MonthView> result = await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default);

        result.IsSuccess.Should().BeTrue();
        MonthClosingView closing = result.Value.Closing!;
        closing.Total.Should().Be(1000m);
        closing.DueDate.Should().Be(DueDate);
        closing.ClosedAt.Should().Be(FinanceTestKit.Now);
        closing.Basis.Should().Be(ApportionmentBasisKind.IdealFraction);
        closing.Charges.Select(charge => (charge.UnitId, charge.Number, charge.Amount)).Should().Equal(
            (FakeApportionmentBasis.First.UnitId, "101", 600m),
            (FakeApportionmentBasis.Second.UnitId, "102", 400m));
        MonthClosing stored = _kit.Closings.Added.Should().ContainSingle().Subject;
        stored.CondominiumId.Should().Be(_kit.CondominiumId);
        stored.ClosedBy.Should().Be(Manager);
        _kit.Atomic.Runs.Should().Be(1);
    }

    [Fact]
    public async Task Close_LeavesTheEntriesAsTheyWere()
    {
        ExpenseEntry entry = NovemberEntry(600m);

        await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default);

        entry.Amount.Should().Be(600m);
        entry.Status.Should().Be(EntryStatus.Pending);
    }

    [Fact]
    public async Task Close_AMonthAlreadyClosed_Fails()
    {
        NovemberEntry(600m);
        _kit.CloseMonth(M(2026, 11));

        (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Error.Should().Be(ClosingErrors.AlreadyClosed);
        _kit.Closings.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Close_AMonthNeverOpened_Fails()
    {
        (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Error.Should().Be(ClosingErrors.MonthNotOpen);
    }

    [Fact]
    public async Task Close_WithAnAmountStillToInform_Fails()
    {
        NovemberEntry(600m);
        _kit.NewEntry(_kit.NewVariable(_group, M(2026, 9)), M(2026, 11));

        (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Error.Should().Be(ClosingErrors.AmountsPending);
    }

    [Fact]
    public async Task Close_WhenTheApportionmentIsNotReady_FailsWithThatReason()
    {
        NovemberEntry(600m);
        Error notReady = new("condominium.apportionment_not_ready", "Complete as unidades");
        _kit.Basis.Outcome = Result.Failure<ApportionmentBasis>(notReady);

        (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Error.Should().Be(notReady);
        _kit.Closings.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Close_ADueDateBeforeTheMonth_Fails()
    {
        NovemberEntry(600m);

        (await CloseHandler().Handle(new CloseMonthCommand("2026-11", new DateOnly(2026, 10, 31), Manager), default)).Error.Should().Be(ClosingErrors.DueDateInvalid);
    }

    [Fact]
    public async Task Close_InvalidMonthOrNoCondominium_Fails()
    {
        (await CloseHandler().Handle(new CloseMonthCommand("novembro", DueDate, Manager), default)).Error.Should().Be(MonthErrors.Invalid);

        _kit.WithoutATenant();

        (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- reopen ----

    [Fact]
    public async Task Reopen_DeletesTheClosingAndItsCharges_AndReturnsTheMonthOpen()
    {
        NovemberEntry(600m);
        MonthClosing closing = _kit.CloseMonth(M(2026, 11));

        Result<MonthView> result = await ReopenHandler().Handle(new ReopenMonthCommand("2026-11"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Closing.Should().BeNull();
        result.Value.Entries.Should().ContainSingle();
        _kit.Closings.Removed.Should().ContainSingle().Which.Should().BeSameAs(closing);
        _kit.Closings.All.Should().BeEmpty();
        _kit.Atomic.Runs.Should().Be(1);
    }

    [Fact]
    public async Task Reopen_AMonthThatIsNotClosed_Fails()
    {
        (await ReopenHandler().Handle(new ReopenMonthCommand("2026-11"), default)).Error.Should().Be(ClosingErrors.NotClosed);
    }

    [Fact]
    public async Task Reopen_InvalidMonthOrNoCondominium_Fails()
    {
        (await ReopenHandler().Handle(new ReopenMonthCommand("11/2026"), default)).Error.Should().Be(MonthErrors.Invalid);

        _kit.WithoutATenant();

        (await ReopenHandler().Handle(new ReopenMonthCommand("2026-11"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task CloseAgainAfterReopening_RecomputesTheCharges()
    {
        ExpenseEntry entry = NovemberEntry(600m);
        await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default);
        await ReopenHandler().Handle(new ReopenMonthCommand("2026-11"), default);
        entry.SetAmount(1000m);

        Result<MonthView> result = await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default);

        result.Value.Closing!.Charges.Select(charge => charge.Amount).Should().Equal(600m, 400m);
    }
}
