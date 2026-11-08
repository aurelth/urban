using FluentAssertions;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class ExpenseTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid GroupId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static YearMonth M(int year, int month) => YearMonth.Create(year, month).Value;

    private static Expense Single(decimal amount = 250m, YearMonth? month = null) =>
        Expense.CreateSingle(CondominiumId, GroupId, "Troca da lâmpada", "Eletro Silva", month ?? M(2026, 9), amount, Now).Value;

    private static Expense Fixed(decimal amount = 300m, YearMonth? start = null, YearMonth? end = null) =>
        Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", null, start ?? M(2026, 9), end, amount, Now).Value;

    private static Expense Variable(YearMonth? start = null) =>
        Expense.CreateVariable(CondominiumId, GroupId, "Energia elétrica", "Enel", start ?? M(2026, 9), null, Now).Value;

    private static Expense Installment(decimal total = 600m, int count = 6, int current = 1, YearMonth? first = null) =>
        Expense.CreateInstallment(CondominiumId, GroupId, "Obra da garagem", "Benedett", total, count, first ?? M(2026, 9), current, Now).Value;

    // ---- common rules ----

    [Fact]
    public void Create_Single_StoresEverythingAndStartsActive()
    {
        Expense expense = Single();

        expense.Id.Should().NotBe(Guid.Empty);
        expense.CondominiumId.Should().Be(CondominiumId);
        expense.GroupId.Should().Be(GroupId);
        expense.Description.Should().Be("Troca da lâmpada");
        expense.Supplier.Should().Be("Eletro Silva");
        expense.Kind.Should().Be(ExpenseKind.Single);
        expense.StartMonth.Should().Be(M(2026, 9));
        expense.CreatedAt.Should().Be(Now);
        expense.AmountFor(M(2026, 9)).Should().Be(250m);
    }

    [Fact]
    public void Create_NormalizesDescriptionAndSupplier_AndEmptySupplierBecomesNull()
    {
        Expense expense = Expense.CreateSingle(CondominiumId, GroupId, "  Troca   da lâmpada ", "   ", M(2026, 9), 10m, Now).Value;

        expense.Description.Should().Be("Troca da lâmpada");
        expense.Supplier.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithoutDescription_Fails(string? description)
    {
        Expense.CreateSingle(CondominiumId, GroupId, description, null, M(2026, 9), 10m, Now)
            .Error.Should().Be(ExpenseErrors.DescriptionInvalid);
    }

    [Fact]
    public void Create_DescriptionAndSupplierLimits_AreOneHundredAndTwentyCharacters()
    {
        Expense.CreateSingle(CondominiumId, GroupId, new string('a', 120), new string('b', 120), M(2026, 9), 10m, Now)
            .IsSuccess.Should().BeTrue();
        Expense.CreateSingle(CondominiumId, GroupId, new string('a', 121), null, M(2026, 9), 10m, Now)
            .Error.Should().Be(ExpenseErrors.DescriptionInvalid);
        Expense.CreateSingle(CondominiumId, GroupId, "Troca", new string('b', 121), M(2026, 9), 10m, Now)
            .Error.Should().Be(ExpenseErrors.SupplierInvalid);
    }

    [Fact]
    public void Create_WithoutCondominiumOrGroup_Fails()
    {
        Expense.CreateSingle(Guid.Empty, GroupId, "Troca", null, M(2026, 9), 10m, Now)
            .Error.Should().Be(ExpenseErrors.CondominiumRequired);
        Expense.CreateSingle(CondominiumId, Guid.Empty, "Troca", null, M(2026, 9), 10m, Now)
            .Error.Should().Be(ExpenseErrors.GroupRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    [InlineData(10000000)]
    public void Create_AnInvalidAmount_Fails(double amount)
    {
        Expense.CreateSingle(CondominiumId, GroupId, "Troca", null, M(2026, 9), (decimal)amount, Now)
            .Error.Should().Be(ExpenseErrors.AmountInvalid);
        Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", null, M(2026, 9), null, (decimal)amount, Now)
            .Error.Should().Be(ExpenseErrors.AmountInvalid);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(9999999.99)]
    public void Create_TheAmountLimits_Work(double amount)
    {
        Expense.CreateSingle(CondominiumId, GroupId, "Troca", null, M(2026, 9), (decimal)amount, Now)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Edit_ChangesDescriptionSupplierAndGroup_AndInvalidValuesChangeNothing()
    {
        Expense expense = Single();
        Guid otherGroup = Guid.CreateVersion7();

        expense.Edit(" Troca da lâmpada LED ", "Eletro Brasil", otherGroup).IsSuccess.Should().BeTrue();

        expense.Description.Should().Be("Troca da lâmpada LED");
        expense.Supplier.Should().Be("Eletro Brasil");
        expense.GroupId.Should().Be(otherGroup);

        expense.Edit("", null, otherGroup).Error.Should().Be(ExpenseErrors.DescriptionInvalid);
        expense.Edit("x", new string('a', 121), otherGroup).Error.Should().Be(ExpenseErrors.SupplierInvalid);
        expense.Edit("x", null, Guid.Empty).Error.Should().Be(ExpenseErrors.GroupRequired);
        expense.Description.Should().Be("Troca da lâmpada LED");
    }

    // ---- single ----

    [Fact]
    public void Single_IsDueOnlyInItsMonth()
    {
        Expense expense = Single(month: M(2026, 9));

        expense.IsDueIn(M(2026, 8)).Should().BeFalse();
        expense.IsDueIn(M(2026, 9)).Should().BeTrue();
        expense.IsDueIn(M(2026, 10)).Should().BeFalse();
        expense.LastMonth.Should().Be(M(2026, 9));
    }

    // ---- fixed ----

    [Fact]
    public void Fixed_WithoutEnd_IsDueFromTheStartForever()
    {
        Expense expense = Fixed(start: M(2026, 9));

        expense.Kind.Should().Be(ExpenseKind.Fixed);
        expense.IsDueIn(M(2026, 8)).Should().BeFalse();
        expense.IsDueIn(M(2026, 9)).Should().BeTrue();
        expense.IsDueIn(M(2040, 1)).Should().BeTrue();
        expense.LastMonth.Should().BeNull();
    }

    [Fact]
    public void Fixed_WithEnd_IsNotDueAfterIt()
    {
        Expense expense = Fixed(start: M(2026, 9), end: M(2026, 12));

        expense.IsDueIn(M(2026, 12)).Should().BeTrue();
        expense.IsDueIn(M(2027, 1)).Should().BeFalse();
        expense.LastMonth.Should().Be(M(2026, 12));
    }

    [Fact]
    public void Fixed_EndBeforeStart_Fails()
    {
        Expense.CreateFixed(CondominiumId, GroupId, "Zeladoria", null, M(2026, 9), M(2026, 8), 300m, Now)
            .Error.Should().Be(ExpenseErrors.EndBeforeStart);
    }

    [Fact]
    public void Fixed_ChangeAmountFrom_AppliesFromThatMonthAndKeepsHistory()
    {
        Expense expense = Fixed(300m, start: M(2026, 9));

        expense.ChangeAmountFrom(M(2027, 1), 330m).IsSuccess.Should().BeTrue();

        expense.AmountFor(M(2026, 9)).Should().Be(300m);
        expense.AmountFor(M(2026, 12)).Should().Be(300m);
        expense.AmountFor(M(2027, 1)).Should().Be(330m);
        expense.AmountFor(M(2028, 6)).Should().Be(330m);
        expense.AmountChanges.Should().HaveCount(2);
    }

    [Fact]
    public void Fixed_SeveralChanges_UseTheLatestOneThatAlreadyStarted()
    {
        Expense expense = Fixed(300m, start: M(2026, 9));
        expense.ChangeAmountFrom(M(2027, 6), 360m);
        expense.ChangeAmountFrom(M(2027, 1), 330m);

        expense.AmountFor(M(2027, 3)).Should().Be(330m);
        expense.AmountFor(M(2027, 6)).Should().Be(360m);
    }

    [Fact]
    public void Fixed_ChangeAmountFrom_InAMonthThatAlreadyHasAChange_ReplacesIt()
    {
        Expense expense = Fixed(300m, start: M(2026, 9));
        expense.ChangeAmountFrom(M(2027, 1), 330m);

        expense.ChangeAmountFrom(M(2027, 1), 345.50m).IsSuccess.Should().BeTrue();

        expense.AmountFor(M(2027, 1)).Should().Be(345.50m);
        expense.AmountChanges.Should().HaveCount(2);
    }

    [Fact]
    public void Fixed_ChangeAmountFrom_OnTheStartMonth_ReplacesTheInitialAmount()
    {
        Expense expense = Fixed(300m, start: M(2026, 9));

        expense.ChangeAmountFrom(M(2026, 9), 310m).IsSuccess.Should().BeTrue();

        expense.AmountFor(M(2026, 9)).Should().Be(310m);
        expense.AmountChanges.Should().HaveCount(1);
    }

    [Fact]
    public void Fixed_ChangeAmountFrom_BeforeTheStart_Fails()
    {
        Fixed(300m, start: M(2026, 9)).ChangeAmountFrom(M(2026, 8), 310m).Error.Should().Be(ExpenseErrors.ChangeBeforeStart);
    }

    [Fact]
    public void Fixed_ChangeAmountFrom_WithAnInvalidAmount_Fails()
    {
        Fixed().ChangeAmountFrom(M(2027, 1), 0m).Error.Should().Be(ExpenseErrors.AmountInvalid);
    }

    [Fact]
    public void ChangeAmountFrom_OnOtherKinds_Fails()
    {
        Single().ChangeAmountFrom(M(2026, 10), 10m).Error.Should().Be(ExpenseErrors.NotFixed);
        Variable().ChangeAmountFrom(M(2026, 10), 10m).Error.Should().Be(ExpenseErrors.NotFixed);
        Installment().ChangeAmountFrom(M(2026, 10), 10m).Error.Should().Be(ExpenseErrors.NotFixed);
    }

    // ---- variable ----

    [Fact]
    public void Variable_HasNoAmountAndIsDueFromTheStart()
    {
        Expense expense = Variable(start: M(2026, 9));

        expense.Kind.Should().Be(ExpenseKind.Variable);
        expense.AmountFor(M(2026, 9)).Should().BeNull();
        expense.IsDueIn(M(2026, 8)).Should().BeFalse();
        expense.IsDueIn(M(2026, 9)).Should().BeTrue();
        expense.IsDueIn(M(2035, 1)).Should().BeTrue();
    }

    // ---- installment ----

    [Fact]
    public void Installment_SplitsTheTotal_AndStartsFromTheCurrentParcel()
    {
        Expense expense = Installment(total: 600m, count: 6, current: 4, first: M(2026, 9));

        expense.Kind.Should().Be(ExpenseKind.Installment);
        expense.InstallmentCount.Should().Be(6);
        expense.CurrentInstallment.Should().Be(4);
        expense.Installments.Should().HaveCount(6);
        expense.Installments.Select(parcel => parcel.Amount).Should().OnlyContain(amount => amount == 100m);
    }

    [Fact]
    public void Installment_MapsEachRemainingParcelToAConsecutiveMonth()
    {
        Expense expense = Installment(total: 600m, count: 6, current: 4, first: M(2026, 9));

        expense.IsDueIn(M(2026, 8)).Should().BeFalse();
        expense.IsDueIn(M(2026, 9)).Should().BeTrue();
        expense.IsDueIn(M(2026, 11)).Should().BeTrue();
        expense.IsDueIn(M(2026, 12)).Should().BeFalse();
        expense.LastMonth.Should().Be(M(2026, 11));
        expense.StartMonth.Should().Be(M(2026, 9));
    }

    [Fact]
    public void Installment_FromTheFirstParcel_RunsForAllOfThem()
    {
        Expense expense = Installment(total: 600m, count: 6, current: 1, first: M(2026, 11));

        expense.LastMonth.Should().Be(M(2027, 4));
        expense.IsDueIn(M(2027, 4)).Should().BeTrue();
        expense.IsDueIn(M(2027, 5)).Should().BeFalse();
    }

    [Fact]
    public void Installment_PutsTheCentsDifferenceOnTheLastParcel()
    {
        Expense expense = Installment(total: 100m, count: 3);

        expense.Installments.Select(parcel => parcel.Amount).Should().Equal(33.33m, 33.33m, 33.34m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Installment_ACurrentParcelOutsideTheRange_Fails(int current)
    {
        Expense.CreateInstallment(CondominiumId, GroupId, "Obra", null, 600m, 6, M(2026, 9), current, Now)
            .Error.Should().Be(ExpenseErrors.CurrentInstallmentInvalid);
    }

    [Fact]
    public void Installment_InvalidTotalOrCount_FailsWithTheSplitErrors()
    {
        Expense.CreateInstallment(CondominiumId, GroupId, "Obra", null, 0m, 6, M(2026, 9), 1, Now)
            .Error.Should().Be(ExpenseErrors.AmountInvalid);
        Expense.CreateInstallment(CondominiumId, GroupId, "Obra", null, 600m, 1, M(2026, 9), 1, Now)
            .Error.Should().Be(ExpenseErrors.InstallmentCountInvalid);
    }

    [Fact]
    public void Installment_AmountFor_ReturnsTheParcelOfThatMonth()
    {
        Expense expense = Installment(total: 100m, count: 3, current: 1, first: M(2026, 9));

        expense.AmountFor(M(2026, 9)).Should().Be(33.33m);
        expense.AmountFor(M(2026, 10)).Should().Be(33.33m);
        expense.AmountFor(M(2026, 11)).Should().Be(33.34m);
        expense.AmountFor(M(2026, 12)).Should().BeNull();
    }

    [Fact]
    public void Installment_SetInstallmentAmount_ChangesOnlyThatParcel()
    {
        Expense expense = Installment(total: 600m, count: 6, current: 1, first: M(2026, 9));

        expense.SetInstallmentAmount(3, 120.40m).IsSuccess.Should().BeTrue();

        expense.AmountFor(M(2026, 10)).Should().Be(100m);
        expense.AmountFor(M(2026, 11)).Should().Be(120.40m);
        expense.AmountFor(M(2026, 12)).Should().Be(100m);
    }

    [Fact]
    public void Installment_SetInstallmentAmount_Invalid_Fails()
    {
        Expense expense = Installment();

        expense.SetInstallmentAmount(0, 10m).Error.Should().Be(ExpenseErrors.InstallmentNotFound);
        expense.SetInstallmentAmount(7, 10m).Error.Should().Be(ExpenseErrors.InstallmentNotFound);
        expense.SetInstallmentAmount(2, 0m).Error.Should().Be(ExpenseErrors.AmountInvalid);
    }

    [Fact]
    public void SetInstallmentAmount_OnOtherKinds_Fails()
    {
        Fixed().SetInstallmentAmount(1, 10m).Error.Should().Be(ExpenseErrors.NotInstallment);
    }

    [Fact]
    public void Installment_TotalOfTheRemainingParcels_SumsTheParcelsStillToCome()
    {
        Expense expense = Installment(total: 600m, count: 6, current: 4);

        expense.RemainingTotal.Should().Be(300m);
    }

    // ---- ending ----

    [Fact]
    public void End_OnFixed_StopsTheScheduleAfterThatMonth()
    {
        Expense expense = Fixed(start: M(2026, 9));

        expense.End(M(2027, 2)).IsSuccess.Should().BeTrue();

        expense.LastMonth.Should().Be(M(2027, 2));
        expense.IsDueIn(M(2027, 2)).Should().BeTrue();
        expense.IsDueIn(M(2027, 3)).Should().BeFalse();
    }

    [Fact]
    public void End_OnVariable_Works_AndBeforeTheStartFails()
    {
        Expense expense = Variable(start: M(2026, 9));

        expense.End(M(2026, 8)).Error.Should().Be(ExpenseErrors.EndBeforeStart);
        expense.End(M(2026, 9)).IsSuccess.Should().BeTrue();
        expense.IsDueIn(M(2026, 10)).Should().BeFalse();
    }

    [Fact]
    public void End_OnSingleOrInstallment_Fails()
    {
        Single().End(M(2026, 9)).Error.Should().Be(ExpenseErrors.EndNotAllowed);
        Installment().End(M(2026, 9)).Error.Should().Be(ExpenseErrors.EndNotAllowed);
    }

    [Fact]
    public void IsEnded_IsTrueOnlyWhenTheLastMonthIsBeforeTheReference()
    {
        Expense ending = Fixed(start: M(2026, 9), end: M(2026, 12));

        ending.IsEnded(M(2026, 12)).Should().BeFalse();
        ending.IsEnded(M(2027, 1)).Should().BeTrue();
        Fixed().IsEnded(M(2099, 1)).Should().BeFalse();
        Installment(total: 600m, count: 6, current: 4, first: M(2026, 9)).IsEnded(M(2026, 12)).Should().BeTrue();
        Installment(total: 600m, count: 6, current: 4, first: M(2026, 9)).IsEnded(M(2026, 11)).Should().BeFalse();
    }

    // ---- entry generation ----

    [Fact]
    public void GenerateEntry_Single_CarriesTheAmountAndSnapshotsTheDescription()
    {
        Expense expense = Single(250m, M(2026, 9));

        Result<ExpenseEntry> result = expense.GenerateEntry(M(2026, 9), Now);

        result.IsSuccess.Should().BeTrue();
        ExpenseEntry entry = result.Value;
        entry.ExpenseId.Should().Be(expense.Id);
        entry.CondominiumId.Should().Be(CondominiumId);
        entry.GroupId.Should().Be(GroupId);
        entry.Month.Should().Be(M(2026, 9));
        entry.Description.Should().Be("Troca da lâmpada");
        entry.Amount.Should().Be(250m);
        entry.IsPending.Should().BeFalse();
        entry.Label.Should().BeNull();
        entry.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void GenerateEntry_Fixed_UsesTheAmountInEffectInThatMonth()
    {
        Expense expense = Fixed(300m, start: M(2026, 9));
        expense.ChangeAmountFrom(M(2027, 1), 330m);

        expense.GenerateEntry(M(2026, 12), Now).Value.Amount.Should().Be(300m);
        expense.GenerateEntry(M(2027, 1), Now).Value.Amount.Should().Be(330m);
    }

    [Fact]
    public void GenerateEntry_Variable_StartsPendingWithoutAnAmount()
    {
        ExpenseEntry entry = Variable().GenerateEntry(M(2026, 9), Now).Value;

        entry.Amount.Should().BeNull();
        entry.IsPending.Should().BeTrue();
    }

    [Fact]
    public void GenerateEntry_Installment_LabelsTheParcelAndUsesItsAmount()
    {
        Expense expense = Installment(total: 600m, count: 6, current: 4, first: M(2026, 9));

        ExpenseEntry first = expense.GenerateEntry(M(2026, 9), Now).Value;
        ExpenseEntry last = expense.GenerateEntry(M(2026, 11), Now).Value;

        first.Label.Should().Be("4/6");
        first.Amount.Should().Be(100m);
        last.Label.Should().Be("6/6");
    }

    [Fact]
    public void GenerateEntry_WhenNotDue_Fails()
    {
        Single(month: M(2026, 9)).GenerateEntry(M(2026, 10), Now).Error.Should().Be(ExpenseErrors.NotDueInMonth);
        Fixed(start: M(2026, 9)).GenerateEntry(M(2026, 8), Now).Error.Should().Be(ExpenseErrors.NotDueInMonth);
        Installment(total: 600m, count: 6, current: 4, first: M(2026, 9)).GenerateEntry(M(2026, 12), Now)
            .Error.Should().Be(ExpenseErrors.NotDueInMonth);
    }

    [Fact]
    public void GenerateEntry_UsesTheCurrentDescription_NotTheOldOne()
    {
        Expense expense = Fixed();
        expense.Edit("Zeladoria e limpeza", null, GroupId);

        expense.GenerateEntry(M(2026, 10), Now).Value.Description.Should().Be("Zeladoria e limpeza");
    }
}
