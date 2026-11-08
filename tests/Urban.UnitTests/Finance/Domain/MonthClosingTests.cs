using FluentAssertions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Domain;

public class MonthClosingTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid GroupId = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly YearMonth November = YearMonth.Create(2026, 11).Value;
    private static readonly DateOnly DueDate = new(2026, 12, 10);

    private static readonly ApportionmentUnit Unit101 = new(Guid.CreateVersion7(), null, "101", 0.6m);
    private static readonly ApportionmentUnit Unit102 = new(Guid.CreateVersion7(), "A", "102", 0.4m);

    private static ApportionmentBasis Basis(params ApportionmentUnit[] units) => new(ApportionmentBasisKind.IdealFraction, units);

    private static ExpenseEntry Fixed(decimal amount, string description = "Zeladoria") =>
        Expense.CreateFixed(CondominiumId, GroupId, description, null, November, null, amount, 10, Now).Value.GenerateEntry(November, Now).Value;

    private static ExpenseEntry Variable() =>
        Expense.CreateVariable(CondominiumId, GroupId, "Energia elétrica", "Enel", November, null, 10, Now).Value.GenerateEntry(November, Now).Value;

    private static Result<MonthClosing> Close(IReadOnlyList<ExpenseEntry> entries, ApportionmentBasis? basis = null, DateOnly? dueDate = null) =>
        MonthClosing.Close(CondominiumId, November, dueDate ?? DueDate, Manager, Now, entries, basis ?? Basis(Unit101, Unit102));

    [Fact]
    public void Close_SplitsTheMonthTotalByWeightAndFreezesTheDetails()
    {
        Result<MonthClosing> result = Close([Fixed(600m), Fixed(400m, "Limpeza")]);

        result.IsSuccess.Should().BeTrue();
        MonthClosing closing = result.Value;
        closing.CondominiumId.Should().Be(CondominiumId);
        closing.Month.Should().Be(November);
        closing.Total.Should().Be(1000m);
        closing.DueDate.Should().Be(DueDate);
        closing.BasisKind.Should().Be(ApportionmentBasisKind.IdealFraction);
        closing.ClosedBy.Should().Be(Manager);
        closing.ClosedAt.Should().Be(Now);
        closing.Charges.Select(c => (c.UnitId, c.Block, c.Number, c.Weight, c.Amount)).Should().Equal(
            (Unit101.UnitId, (string?)null, "101", 0.6m, 600m),
            (Unit102.UnitId, (string?)"A", "102", 0.4m, 400m));
        closing.Charges.Should().OnlyContain(c => c.ClosingId == closing.Id && c.CondominiumId == CondominiumId);
        closing.Charges.Select(c => c.Position).Should().Equal(0, 1);
    }

    [Fact]
    public void Close_TheChargesAlwaysAddUpToTheTotal()
    {
        ApportionmentUnit third = new(Guid.CreateVersion7(), null, "103", 1m);
        ApportionmentBasis basis = new(ApportionmentBasisKind.EqualShare, [Unit101 with { Weight = 1m }, Unit102 with { Weight = 1m }, third]);

        MonthClosing closing = Close([Fixed(100m)], basis).Value;

        closing.Charges.Select(c => c.Amount).Should().Equal(33.34m, 33.33m, 33.33m);
        closing.Charges.Sum(c => c.Amount).Should().Be(closing.Total);
    }

    [Fact]
    public void Close_LeavesCanceledEntriesOutOfTheTotal()
    {
        ExpenseEntry canceled = Fixed(500m, "Obra");
        canceled.SetStatus(EntryStatus.Canceled);

        MonthClosing closing = Close([Fixed(1000m), canceled]).Value;

        closing.Total.Should().Be(1000m);
    }

    [Fact]
    public void Close_CanceledVariableWithoutAmount_DoesNotBlockTheClosing()
    {
        ExpenseEntry canceled = Variable();
        canceled.SetStatus(EntryStatus.Canceled);

        Close([Fixed(1000m), canceled]).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Close_PaidAndPendingEntriesBothCount()
    {
        ExpenseEntry paid = Fixed(300m, "Água");
        paid.SetStatus(EntryStatus.Paid);

        Close([Fixed(700m), paid]).Value.Total.Should().Be(1000m);
    }

    [Fact]
    public void Close_WithoutEntries_FailsAsMonthNotOpen()
    {
        Close([]).Error.Should().Be(ClosingErrors.MonthNotOpen);
    }

    [Fact]
    public void Close_WithAVariableEntryStillWithoutAmount_Fails()
    {
        Close([Fixed(1000m), Variable()]).Error.Should().Be(ClosingErrors.AmountsPending);
    }

    [Fact]
    public void Close_WhenEverythingIsCanceled_FailsAsNothingToCharge()
    {
        ExpenseEntry canceled = Fixed(100m);
        canceled.SetStatus(EntryStatus.Canceled);

        Close([canceled]).Error.Should().Be(ClosingErrors.NothingToCharge);
    }

    [Fact]
    public void Close_ADueDateBeforeTheMonthStarts_Fails()
    {
        Close([Fixed(100m)], dueDate: new DateOnly(2026, 10, 31)).Error.Should().Be(ClosingErrors.DueDateInvalid);
    }

    [Fact]
    public void Close_ADueDateOnTheFirstDayOfTheMonth_IsAccepted()
    {
        Close([Fixed(100m)], dueDate: new DateOnly(2026, 11, 1)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Close_WithoutUnits_FailsAsNoWeights()
    {
        Close([Fixed(100m)], Basis()).Error.Should().Be(ClosingErrors.NoWeights);
    }
}
