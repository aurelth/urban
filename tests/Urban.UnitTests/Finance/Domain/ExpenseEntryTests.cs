using FluentAssertions;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class ExpenseEntryTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid GroupId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static YearMonth M(int year, int month) => YearMonth.Create(year, month).Value;

    private static ExpenseEntry PendingEntry() =>
        Expense.CreateVariable(CondominiumId, GroupId, "Energia elétrica", "Enel", M(2026, 9), null, Now)
            .Value.GenerateEntry(M(2026, 9), Now).Value;

    [Fact]
    public void SetAmount_FillsAPendingEntry()
    {
        ExpenseEntry entry = PendingEntry();

        Result result = entry.SetAmount(412.37m);

        result.IsSuccess.Should().BeTrue();
        entry.Amount.Should().Be(412.37m);
        entry.IsPending.Should().BeFalse();
    }

    [Fact]
    public void SetAmount_CanCorrectAFilledEntry()
    {
        ExpenseEntry entry = PendingEntry();
        entry.SetAmount(412.37m);

        entry.SetAmount(400m).IsSuccess.Should().BeTrue();

        entry.Amount.Should().Be(400m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.234)]
    [InlineData(10000000)]
    public void SetAmount_Invalid_FailsAndKeepsTheOldValue(double amount)
    {
        ExpenseEntry entry = PendingEntry();
        entry.SetAmount(100m);

        entry.SetAmount((decimal)amount).Error.Should().Be(ExpenseErrors.AmountInvalid);
        entry.Amount.Should().Be(100m);
    }

    [Fact]
    public void SetAmount_OnOneEntry_DoesNotChangeTheSchedule()
    {
        Expense expense = Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", null, M(2026, 9), null, 300m, Now).Value;
        ExpenseEntry september = expense.GenerateEntry(M(2026, 9), Now).Value;

        september.SetAmount(310m);

        expense.AmountFor(M(2026, 9)).Should().Be(300m);
        expense.GenerateEntry(M(2026, 10), Now).Value.Amount.Should().Be(300m);
    }
}
