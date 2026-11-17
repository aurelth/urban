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
        Expense.CreateVariable(CondominiumId, GroupId, "Energia elétrica", "Enel", M(2026, 9), null, 10, Now)
            .Value.GenerateEntry(M(2026, 9), Now).Value;

    [Fact]
    public void SetAmount_FillsAPendingEntry()
    {
        ExpenseEntry entry = PendingEntry();

        Result result = entry.SetAmount(412.37m);

        result.IsSuccess.Should().BeTrue();
        entry.Amount.Should().Be(412.37m);
        entry.NeedsAmount.Should().BeFalse();
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
        Expense expense = Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", null, M(2026, 9), null, 300m, 10, Now).Value;
        ExpenseEntry september = expense.GenerateEntry(M(2026, 9), Now).Value;

        september.SetAmount(310m);

        expense.AmountFor(M(2026, 9)).Should().Be(300m);
        expense.GenerateEntry(M(2026, 10), Now).Value.Amount.Should().Be(300m);
    }

    // ---- status ----

    private static ExpenseEntry FixedEntry(int dueDay = 10) =>
        Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", null, M(2026, 9), null, 300m, dueDay, Now)
            .Value.GenerateEntry(M(2026, 11), Now).Value;

    [Fact]
    public void ANewEntry_IsPending()
    {
        FixedEntry().Status.Should().Be(EntryStatus.Pending);
    }

    [Fact]
    public void StatusOn_IsPendingUntilTheEndOfTheDueDay_ThenOverdue()
    {
        ExpenseEntry entry = FixedEntry(dueDay: 10);

        entry.StatusOn(new DateOnly(2026, 11, 9)).Should().Be(EntryStatus.Pending);
        entry.StatusOn(new DateOnly(2026, 11, 10)).Should().Be(EntryStatus.Pending);
        entry.StatusOn(new DateOnly(2026, 11, 11)).Should().Be(EntryStatus.Overdue);
        entry.StatusOn(new DateOnly(2027, 3, 1)).Should().Be(EntryStatus.Overdue);
        entry.Status.Should().Be(EntryStatus.Pending);
    }

    [Fact]
    public void SetStatus_Paid_StaysPaidEvenAfterTheDueDate()
    {
        ExpenseEntry entry = FixedEntry();

        entry.SetStatus(EntryStatus.Paid).IsSuccess.Should().BeTrue();

        entry.Status.Should().Be(EntryStatus.Paid);
        entry.StatusOn(new DateOnly(2027, 1, 1)).Should().Be(EntryStatus.Paid);
    }

    [Fact]
    public void SetStatus_PaidWithoutAnAmount_FailsAndStaysPending()
    {
        ExpenseEntry entry = PendingEntry();

        entry.SetStatus(EntryStatus.Paid).Error.Should().Be(ExpenseErrors.AmountRequiredToPay);

        entry.Status.Should().Be(EntryStatus.Pending);
    }

    [Fact]
    public void SetStatus_PaidAfterTheAmountIsFilled_Succeeds()
    {
        ExpenseEntry entry = PendingEntry();
        entry.SetAmount(412.37m);

        entry.SetStatus(EntryStatus.Paid).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SetStatus_CanceledWithoutAnAmount_Succeeds_AndNeverTurnsOverdue()
    {
        ExpenseEntry entry = PendingEntry();

        entry.SetStatus(EntryStatus.Canceled).IsSuccess.Should().BeTrue();

        entry.StatusOn(new DateOnly(2027, 1, 1)).Should().Be(EntryStatus.Canceled);
    }

    [Fact]
    public void SetStatus_Pending_UndoesPaidAndCanceled()
    {
        ExpenseEntry entry = FixedEntry();
        entry.SetStatus(EntryStatus.Paid);

        entry.SetStatus(EntryStatus.Pending).IsSuccess.Should().BeTrue();
        entry.Status.Should().Be(EntryStatus.Pending);

        entry.SetStatus(EntryStatus.Canceled);
        entry.SetStatus(EntryStatus.Pending).IsSuccess.Should().BeTrue();
        entry.StatusOn(new DateOnly(2027, 1, 1)).Should().Be(EntryStatus.Overdue);
    }

    [Fact]
    public void SetStatus_Overdue_IsRefused_BecauseItIsCalculated()
    {
        ExpenseEntry entry = FixedEntry();

        entry.SetStatus(EntryStatus.Overdue).Error.Should().Be(ExpenseErrors.StatusInvalid);

        entry.Status.Should().Be(EntryStatus.Pending);
    }

    [Fact]
    public void SetAmount_OnAPaidEntry_KeepsItPaid()
    {
        ExpenseEntry entry = FixedEntry();
        entry.SetStatus(EntryStatus.Paid);

        entry.SetAmount(310m).IsSuccess.Should().BeTrue();

        entry.Status.Should().Be(EntryStatus.Paid);
    }

    // ---- fornecedor e observação do lançamento ----

    [Fact]
    public void GenerateEntry_CarriesTheSupplierOfTheExpense_AndStartsWithoutANote()
    {
        ExpenseEntry entry = PendingEntry();

        entry.Supplier.Should().Be("Enel");
        entry.Note.Should().BeNull();
    }

    [Fact]
    public void GenerateEntry_OfAnExpenseWithoutSupplier_HasNone()
    {
        Expense expense = Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", null, M(2026, 9), null, 300m, 10, Now).Value;

        expense.GenerateEntry(M(2026, 9), Now).Value.Supplier.Should().BeNull();
    }

    [Fact]
    public void TheEntryKeepsTheSupplierItWasBornWith_WhenTheExpenseChangesLater()
    {
        Expense expense = Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", "Limpa Tudo", M(2026, 9), null, 300m, 10, Now).Value;
        ExpenseEntry september = expense.GenerateEntry(M(2026, 9), Now).Value;

        expense.Edit("Zeladoria", "Brilho Total", GroupId, 10).IsSuccess.Should().BeTrue();
        ExpenseEntry october = expense.GenerateEntry(M(2026, 10), Now).Value;

        september.Supplier.Should().Be("Limpa Tudo");
        october.Supplier.Should().Be("Brilho Total");
    }

    [Fact]
    public void SetNote_KeepsTheTrimmedText()
    {
        ExpenseEntry entry = PendingEntry();

        entry.SetNote("  OS 4521, período de 01 a 15/10  ").IsSuccess.Should().BeTrue();

        entry.Note.Should().Be("OS 4521, período de 01 a 15/10");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetNote_Blank_ClearsTheNote(string? blank)
    {
        ExpenseEntry entry = PendingEntry();
        entry.SetNote("OS 1");

        entry.SetNote(blank).IsSuccess.Should().BeTrue();

        entry.Note.Should().BeNull();
    }

    [Fact]
    public void SetNote_AcceptsTwoHundredCharacters_AndRefusesTwoHundredAndOne()
    {
        ExpenseEntry entry = PendingEntry();

        entry.SetNote(new string('a', 200)).IsSuccess.Should().BeTrue();
        entry.SetNote(new string('b', 201)).Error.Should().Be(ExpenseErrors.NoteInvalid);

        entry.Note.Should().Be(new string('a', 200));
    }

    [Theory]
    [InlineData("linha 1\nlinha 2")]
    [InlineData("com\ttabulação")]
    public void SetNote_WithControlCharacters_FailsAndKeepsTheOldNote(string text)
    {
        ExpenseEntry entry = PendingEntry();
        entry.SetNote("OS 1");

        entry.SetNote(text).Error.Should().Be(ExpenseErrors.NoteInvalid);

        entry.Note.Should().Be("OS 1");
    }
}
