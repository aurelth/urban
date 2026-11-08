using FluentAssertions;
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

    private OpenMonthCommandHandler OpenHandler() => new(_kit.Expenses, _kit.Entries, _kit.Groups, _kit.Tenant, _kit.Clock);

    private GetMonthQueryHandler GetHandler() => new(_kit.Entries, _kit.Groups, _kit.Tenant);

    private SetEntryAmountCommandHandler SetHandler() => new(_kit.Entries, _kit.Tenant);

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
        result.Value.PendingCount.Should().Be(1);
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
        second.PendingCount.Should().Be(0);
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
    public async Task Open_FurtherAhead_Fails()
    {
        _kit.NewFixed(_ordinary, M(2026, 9), 300m, "Zeladoria");

        (await OpenHandler().Handle(new OpenMonthCommand("2027-01"), default)).Error.Should().Be(MonthErrors.TooFarAhead);
        _kit.Entries.Added.Should().BeEmpty();
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
        result.Value.IsPending.Should().BeFalse();
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
        entry.IsPending.Should().BeTrue();

        _kit.WithoutATenant();
        (await SetHandler().Handle(new SetEntryAmountCommand(entry.Id, "5", 10m), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }
}
