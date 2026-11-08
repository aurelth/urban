using FluentAssertions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Application.Months;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class MonthHandlersTests
{
    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _ordinary;
    private readonly ExpenseGroup _garages;

    public MonthHandlersTests()
    {
        _ordinary = _kit.NewGroup("Despesas ordinárias");
        _garages = _kit.NewGroup("Manutenção das garagens");
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private OpenMonthCommandHandler OpenHandler() => new(_kit.Expenses, _kit.Entries, _kit.Closings, _kit.Groups, _kit.Tenant, _kit.Clock);

    private GetMonthQueryHandler GetHandler() => new(_kit.Entries, _kit.Closings, _kit.Groups, _kit.Tenant, _kit.Clock);

    private SetEntryAmountCommandHandler SetHandler() => new(_kit.Entries, _kit.Closings, _kit.Tenant, _kit.Clock);

    private SetEntryStatusCommandHandler StatusHandler() => new(_kit.Entries, _kit.Closings, _kit.Tenant, _kit.Clock);

    // ---- open ----

    [Fact]
    public async Task Open_GeneratesTheEntriesOfEveryExpenseDueInTheMonth()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        _kit.NewInstallment(_garages, M(2026, 9), total: 600m, count: 6, current: 4, description: "Obra da garagem");
        _kit.NewSingle(_ordinary, M(2026, 11), 250m, "Troca da lâmpada");

        Result<MonthView> result = await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Month.Should().Be("2026-11");
        result.Value.Entries.Select(entry => entry.Description).Should().Equal("Energia elétrica", "Troca da lâmpada", "Zeladoria", "Obra da garagem");
        result.Value.Total.Should().Be(650m);
        result.Value.NeedsAmountCount.Should().Be(1);
        _kit.Entries.Added.Should().HaveCount(4);
    }

    [Fact]
    public async Task Open_SkipsWhatIsNotDueThatMonth()
    {
        _kit.NewFixed(_ordinary, M(2026, 12), 300m, "Começa depois");
        _kit.NewFixed(_ordinary, M(2026, 1), 300m, "Já acabou", end: M(2026, 10));
        _kit.NewSingle(_ordinary, M(2026, 10), 10m, "De outro mês");
        _kit.NewInstallment(_garages, M(2026, 5), total: 600m, count: 6, current: 4, description: "Parcelas terminadas");

        Result<MonthView> result = await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default);

        result.Value.Entries.Should().BeEmpty();
        result.Value.Total.Should().Be(0m);
        _kit.Entries.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Open_Twice_IsIdempotent_AndKeepsManualEdits()
    {
        _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        MonthView first = (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Value;
        ExpenseEntry energy = _kit.Entries.All.Single(entry => entry.Description == "Energia elétrica");
        energy.SetAmount(412.37m);

        MonthView second = (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Value;

        first.Entries.Should().HaveCount(2);
        second.Entries.Should().HaveCount(2);
        _kit.Entries.Added.Should().HaveCount(2);
        second.Entries.Single(entry => entry.Description == "Energia elétrica").Amount.Should().Be(412.37m);
        second.NeedsAmountCount.Should().Be(0);
    }

    [Fact]
    public async Task Open_AddsOnlyTheEntriesThatAreMissing()
    {
        Expense zeladoria = _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        _kit.NewEntry(zeladoria, M(2026, 11));
        _kit.NewFixed(_ordinary, M(2026, 9), 80m, "Portaria remota");

        MonthView view = (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Value;

        view.Entries.Should().HaveCount(2);
        _kit.Entries.Added.Should().ContainSingle().Which.Description.Should().Be("Portaria remota");
    }

    [Fact]
    public async Task Open_SuggestsThePreviousMonthAmountForPendingEntries()
    {
        Expense energy = _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        ExpenseEntry october = _kit.NewEntry(energy, M(2026, 10));
        october.SetAmount(398.10m);
        _kit.NewVariable(_ordinary, M(2026, 11), "Água do condomínio");

        MonthView view = (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Value;

        view.Entries.Single(entry => entry.Description == "Energia elétrica").SuggestedAmount.Should().Be(398.10m);
        view.Entries.Single(entry => entry.Description == "Água do condomínio").SuggestedAmount.Should().BeNull();
    }

    [Fact]
    public async Task Open_DoesNotSuggestAnythingForFilledEntries()
    {
        Expense energy = _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        _kit.NewEntry(energy, M(2026, 10)).SetAmount(398.10m);
        _kit.NewEntry(energy, M(2026, 11)).SetAmount(410m);

        MonthView view = (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Value;

        view.Entries.Single().SuggestedAmount.Should().BeNull();
    }

    [Fact]
    public async Task Open_TotalsEachGroup()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        _kit.NewInstallment(_garages, M(2026, 9), total: 600m, count: 6, current: 4, description: "Obra da garagem");

        MonthView view = (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Value;

        view.Groups.Should().BeEquivalentTo(
        [
            new GroupTotalView(_ordinary.Id, "Despesas ordinárias", 300m, 1),
            new GroupTotalView(_garages.Id, "Manutenção das garagens", 100m, 0),
        ],
        options => options.WithStrictOrdering());
        view.Total.Should().Be(400m);
    }

    [Fact]
    public async Task Open_TheMonthAfterTheCurrentOne_IsAllowed()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");

        (await OpenHandler().Handle(new OpenMonthCommand("2026-12"), default)).Value.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task Open_UpToFiveYearsAhead_IsAllowed()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");

        (await OpenHandler().Handle(new OpenMonthCommand("2027-01"), default)).IsSuccess.Should().BeTrue();
        (await OpenHandler().Handle(new OpenMonthCommand("2031-11"), default)).Value.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task Open_FurtherThanFiveYearsAhead_Fails()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");

        (await OpenHandler().Handle(new OpenMonthCommand("2031-12"), default)).Error.Should().Be(MonthErrors.TooFarAhead);
        _kit.Entries.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Open_GivesEachEntryItsDueDate_AndStartsPending()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");

        MonthView view = (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Value;

        view.Entries.Single().DueDate.Should().Be(new DateOnly(2026, 11, 10));
        view.Entries.Single().Status.Should().Be(EntryStatus.Pending);
        view.OverdueCount.Should().Be(0);
    }

    [Fact]
    public async Task Open_AMonthAlreadyPast_ShowsItsUnpaidEntriesAsOverdue()
    {
        _kit.NewFixed(_ordinary, M(2026, 8), 300m, "Zeladoria");

        MonthView view = (await OpenHandler().Handle(new OpenMonthCommand("2026-10"), default)).Value;

        view.Entries.Single().DueDate.Should().Be(new DateOnly(2026, 10, 10));
        view.Entries.Single().Status.Should().Be(EntryStatus.Overdue);
        view.OverdueCount.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("11/2026")]
    public async Task Open_WithAnInvalidMonth_Fails(string? month)
    {
        (await OpenHandler().Handle(new OpenMonthCommand(month), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task Open_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Open_WhenTheStoreRefusesTheEntries_ReturnsTheFailure()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        _kit.Entries.AddRangeFailure = FinanceErrors.ConcurrencyConflict;

        (await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
    }

    // ---- get ----

    [Fact]
    public async Task Get_ShowsTheEntriesOfTheMonthWithoutGeneratingAnything()
    {
        Expense fixedExpense = _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        _kit.NewFixed(_ordinary, M(2026, 9), 80m, "Portaria remota");
        _kit.NewEntry(fixedExpense, M(2026, 11));
        _kit.NewEntry(fixedExpense, M(2026, 10));

        Result<MonthView> result = await GetHandler().Handle(new GetMonthQuery("2026-11"), default);

        result.Value.Entries.Should().ContainSingle().Which.Description.Should().Be("Zeladoria");
        result.Value.Entries[0].Version.Should().Be("5");
        _kit.Entries.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_ComputesTheStatusOfEachEntry_AndLeavesCanceledOutOfTheTotals()
    {
        Expense rent = _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        Expense security = _kit.NewFixed(_ordinary, M(2026, 9), 80m, "Portaria remota");
        Expense gardener = _kit.NewFixed(_ordinary, M(2026, 9), 50m, "Jardinagem");
        Expense energy = _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        _kit.NewEntry(rent, M(2026, 10));
        _kit.NewEntry(security, M(2026, 10)).SetStatus(EntryStatus.Paid);
        _kit.NewEntry(gardener, M(2026, 10)).SetStatus(EntryStatus.Canceled);
        _kit.NewEntry(energy, M(2026, 10));

        MonthView view = (await GetHandler().Handle(new GetMonthQuery("2026-10"), default)).Value;

        view.Entries.ToDictionary(entry => entry.Description, entry => entry.Status).Should().Equal(
            new Dictionary<string, EntryStatus>
            {
                ["Zeladoria"] = EntryStatus.Overdue,
                ["Portaria remota"] = EntryStatus.Paid,
                ["Jardinagem"] = EntryStatus.Canceled,
                ["Energia elétrica"] = EntryStatus.Overdue,
            });
        view.Total.Should().Be(380m);
        view.OverdueCount.Should().Be(2);
        view.NeedsAmountCount.Should().Be(1);
        view.Groups.Should().ContainSingle().Which.Total.Should().Be(380m);
    }

    [Fact]
    public async Task Get_ACanceledEntryWithoutAmount_DoesNotCountAsNeedingOne()
    {
        Expense energy = _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        _kit.NewEntry(energy, M(2026, 11)).SetStatus(EntryStatus.Canceled);

        MonthView view = (await GetHandler().Handle(new GetMonthQuery("2026-11"), default)).Value;

        view.NeedsAmountCount.Should().Be(0);
        view.Groups.Should().ContainSingle().Which.NeedsAmountCount.Should().Be(0);
        view.Entries.Single().NeedsAmount.Should().BeTrue();
    }

    [Fact]
    public async Task Get_OfAMonthWithoutEntries_IsEmptyNotAnError()
    {
        Result<MonthView> result = await GetHandler().Handle(new GetMonthQuery("2026-03"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().BeEmpty();
        result.Value.Groups.Should().BeEmpty();
        result.Value.Total.Should().Be(0m);
    }

    [Fact]
    public async Task Get_Failures()
    {
        (await GetHandler().Handle(new GetMonthQuery("x"), default)).Error.Should().Be(MonthErrors.Invalid);
        _kit.WithoutATenant();
        (await GetHandler().Handle(new GetMonthQuery("2026-11"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- set the amount of one entry ----

    [Fact]
    public async Task SetAmount_FillsAPendingEntry_AndReturnsTheNewVersion()
    {
        Expense energy = _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        ExpenseEntry entry = _kit.NewEntry(energy, M(2026, 11));

        Result<ExpenseEntryView> result = await SetHandler().Handle(new SetEntryAmountCommand(entry.Id, "5", 412.37m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(412.37m);
        result.Value.NeedsAmount.Should().BeFalse();
        result.Value.Version.Should().Be("6");
        entry.Amount.Should().Be(412.37m);
    }

    [Fact]
    public async Task SetAmount_ChangesOnlyThatMonth()
    {
        Expense fixedExpense = _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        ExpenseEntry november = _kit.NewEntry(fixedExpense, M(2026, 11));
        ExpenseEntry december = _kit.NewEntry(fixedExpense, M(2026, 12));

        await SetHandler().Handle(new SetEntryAmountCommand(november.Id, "5", 310m), default);

        november.Amount.Should().Be(310m);
        december.Amount.Should().Be(300m);
        fixedExpense.AmountFor(M(2026, 11)).Should().Be(300m);
    }

    [Fact]
    public async Task SetAmount_Failures()
    {
        Expense energy = _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        ExpenseEntry entry = _kit.NewEntry(energy, M(2026, 11));

        (await SetHandler().Handle(new SetEntryAmountCommand(entry.Id, null, 10m), default)).Error.Should().Be(FinanceErrors.VersionRequired);
        (await SetHandler().Handle(new SetEntryAmountCommand(entry.Id, "4", 10m), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await SetHandler().Handle(new SetEntryAmountCommand(Guid.CreateVersion7(), "5", 10m), default)).Error.Should().Be(ExpenseErrors.EntryNotFound);
        (await SetHandler().Handle(new SetEntryAmountCommand(entry.Id, "5", 0m), default)).Error.Should().Be(ExpenseErrors.AmountInvalid);
        entry.NeedsAmount.Should().BeTrue();

        _kit.WithoutATenant();
        (await SetHandler().Handle(new SetEntryAmountCommand(entry.Id, "5", 10m), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- set the payment status of one entry ----

    [Fact]
    public async Task SetStatus_MarksAnEntryAsPaid_AndReturnsTheNewVersion()
    {
        Expense fixedExpense = _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        ExpenseEntry entry = _kit.NewEntry(fixedExpense, M(2026, 11));

        Result<ExpenseEntryView> result = await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Paid), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EntryStatus.Paid);
        result.Value.Version.Should().Be("6");
        entry.Status.Should().Be(EntryStatus.Paid);
    }

    [Fact]
    public async Task SetStatus_GoingBackToPending_OfAnEntryPastDue_ShowsItAsOverdue()
    {
        Expense fixedExpense = _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");
        ExpenseEntry entry = _kit.NewEntry(fixedExpense, M(2026, 10));
        entry.SetStatus(EntryStatus.Paid);

        Result<ExpenseEntryView> result = await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Pending), default);

        entry.Status.Should().Be(EntryStatus.Pending);
        result.Value.Status.Should().Be(EntryStatus.Overdue);
    }

    [Fact]
    public async Task SetStatus_Failures()
    {
        Expense energy = _kit.NewVariable(_ordinary, M(2026, 9), "Energia elétrica");
        ExpenseEntry entry = _kit.NewEntry(energy, M(2026, 11));

        (await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, null, EntryStatus.Canceled), default)).Error.Should().Be(FinanceErrors.VersionRequired);
        (await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "4", EntryStatus.Canceled), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await StatusHandler().Handle(new SetEntryStatusCommand(Guid.CreateVersion7(), "5", EntryStatus.Canceled), default)).Error.Should().Be(ExpenseErrors.EntryNotFound);
        (await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Paid), default)).Error.Should().Be(ExpenseErrors.AmountRequiredToPay);
        (await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Overdue), default)).Error.Should().Be(ExpenseErrors.StatusInvalid);
        entry.Status.Should().Be(EntryStatus.Pending);

        _kit.WithoutATenant();
        (await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Canceled), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- closed months ----

    [Fact]
    public async Task Get_AClosedMonth_ShowsTheClosingAndItsCharges()
    {
        ExpenseEntry entry = _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));

        Result<MonthView> result = await GetHandler().Handle(new GetMonthQuery("2026-11"), default);

        result.Value.Entries.Should().ContainSingle().Which.Id.Should().Be(entry.Id);
        result.Value.Closing.Should().NotBeNull();
        result.Value.Closing!.Total.Should().Be(600m);
        result.Value.Closing.Charges.Sum(charge => charge.Amount).Should().Be(600m);
    }

    [Fact]
    public async Task Get_AnOpenMonth_HasNoClosing()
    {
        _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));

        (await GetHandler().Handle(new GetMonthQuery("2026-11"), default)).Value.Closing.Should().BeNull();
    }

    [Fact]
    public async Task Open_AClosedMonth_GeneratesNothingNew()
    {
        _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));
        _kit.NewFixed(_ordinary, M(2026, 9), 100m, "Jardinagem");

        Result<MonthView> result = await OpenHandler().Handle(new OpenMonthCommand("2026-11"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().ContainSingle();
        result.Value.Closing.Should().NotBeNull();
        _kit.Entries.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task SetAmount_InAClosedMonth_Fails()
    {
        ExpenseEntry entry = _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));

        (await SetHandler().Handle(new SetEntryAmountCommand(entry.Id, "5", 700m), default)).Error.Should().Be(ClosingErrors.MonthIsClosed);
        entry.Amount.Should().Be(600m);
    }

    [Fact]
    public async Task SetAmount_InAnotherMonth_StillWorksWhileThisOneIsClosed()
    {
        _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));
        ExpenseEntry december = _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 100m, "Jardinagem"), M(2026, 12));

        (await SetHandler().Handle(new SetEntryAmountCommand(december.Id, "5", 120m), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SetStatus_PaidInAClosedMonth_StillWorks_BecauseThePaymentDoesNotChangeTheCharges()
    {
        ExpenseEntry entry = _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));

        Result<ExpenseEntryView> result = await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Paid), default);

        result.IsSuccess.Should().BeTrue();
        entry.Status.Should().Be(EntryStatus.Paid);
    }

    [Fact]
    public async Task SetStatus_CancelingInAClosedMonth_Fails_BecauseItWouldChangeTheTotal()
    {
        ExpenseEntry entry = _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));

        (await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Canceled), default)).Error.Should().Be(ClosingErrors.MonthIsClosed);
        entry.Status.Should().Be(EntryStatus.Pending);
    }

    [Fact]
    public async Task SetStatus_RestoringACanceledEntryInAClosedMonth_Fails()
    {
        ExpenseEntry entry = _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 600m), M(2026, 11));
        _kit.NewEntry(_kit.NewFixed(_ordinary, M(2026, 9), 100m, "Jardinagem"), M(2026, 11));
        entry.SetStatus(EntryStatus.Canceled);
        _kit.CloseMonth(M(2026, 11));

        (await StatusHandler().Handle(new SetEntryStatusCommand(entry.Id, "5", EntryStatus.Pending), default)).Error.Should().Be(ClosingErrors.MonthIsClosed);
        entry.Status.Should().Be(EntryStatus.Canceled);
    }
}
