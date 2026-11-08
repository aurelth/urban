using FluentAssertions;
using Urban.Finance.Application.Months;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Settings;
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

    private CloseMonthCommandHandler CloseHandler() => new(_kit.Entries, _kit.Closings, _kit.Groups, _kit.Settings, _kit.Tariffs, _kit.Readings, _kit.Basis, _kit.Tenant, _kit.Atomic, _kit.Clock);

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

    // ---- close with the condominium's rules ----

    private void ConfigureReserveFund(decimal percent) =>
        _kit.Settings.Existing(FinanceSettings.Create(_kit.CondominiumId, percent, FinanceTestKit.Now).Value);

    private ConsumptionTariff ConfigureTariff(string from = "2026-01", bool gas = true) =>
        _kit.Tariffs.Existing(ConsumptionTariff.Create(
            _kit.CondominiumId,
            YearMonth.Parse(from).Value,
            TariffScale.Create([new TariffBand(10m, 47.43m, 0m), new TariffBand(30m, 0m, 8.63m), new TariffBand(null, 0m, 11m)]).Value,
            TariffScale.Create([new TariffBand(10m, 52.63m, 0m), new TariffBand(null, 0m, 9.635m)]).Value,
            100m,
            gas ? GasTariff.Create(2.4m, 15.71m).Value : null,
            FinanceTestKit.Now).Value);

    private void ReadMeters(Guid unit, decimal waterPrevious, decimal waterCurrent, decimal? gasPrevious = null, decimal? gasCurrent = null)
    {
        _kit.Readings.Existing(MeterReading.Create(_kit.CondominiumId, unit, M(2026, 11), ConsumptionService.Water, waterPrevious, waterCurrent, FinanceTestKit.Now).Value);

        if (gasPrevious is { } previous && gasCurrent is { } current)
        {
            _kit.Readings.Existing(MeterReading.Create(_kit.CondominiumId, unit, M(2026, 11), ConsumptionService.Gas, previous, current, FinanceTestKit.Now).Value);
        }
    }

    private static List<(ChargeLineKind Kind, string Label, decimal Amount)> Lines(UnitChargeView charge) =>
        [.. charge.Lines.Select(line => (line.Kind, line.Label, line.Amount))];

    [Fact]
    public async Task Close_NamesEachExpenseLineAfterItsGroup()
    {
        ExpenseGroup garages = _kit.NewGroup("Manutenção das garagens");
        _kit.NewEntry(_kit.NewFixed(garages, M(2026, 9), 100m, "Portão"), M(2026, 11));
        NovemberEntry(900m);

        MonthView view = (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Value;

        Lines(view.Closing!.Charges[0]).Should().Equal(
            (ChargeLineKind.Expenses, "Despesas ordinárias", 540m),
            (ChargeLineKind.Expenses, "Manutenção das garagens", 60m));
    }

    [Fact]
    public async Task Close_AddsTheReserveFundFromTheConfiguredPercentageOverTheMarkedGroupsOnly()
    {
        _group.SetReserveFundBase(true);
        ExpenseGroup garages = _kit.NewGroup("Manutenção das garagens");
        _kit.NewEntry(_kit.NewFixed(garages, M(2026, 9), 100m, "Portão"), M(2026, 11));
        NovemberEntry(900m);
        ConfigureReserveFund(15m);

        MonthView view = (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Value;

        view.Closing!.Charges.Select(charge => charge.Lines.Single(line => line.Kind == ChargeLineKind.ReserveFund).Amount).Should().Equal(81m, 54m);
        view.Closing.Total.Should().Be(1000m);
        view.Closing.Charges.Select(charge => charge.Amount).Should().Equal(681m, 454m);
    }

    [Fact]
    public async Task Close_WithoutAConfiguredPercentage_HasNoReserveFund()
    {
        _group.SetReserveFundBase(true);
        NovemberEntry(900m);

        MonthView view = (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Value;

        view.Closing!.Charges.SelectMany(charge => charge.Lines).Should().NotContain(line => line.Kind == ChargeLineKind.ReserveFund);
    }

    [Fact]
    public async Task Close_ChargesWaterSewageAndGasFromTheTariffAndTheReadings()
    {
        NovemberEntry(1000m);
        ConfigureTariff();
        ReadMeters(FakeApportionmentBasis.First.UnitId, 1551.913m, 1569.562m, 117.825m, 118.677m);
        ReadMeters(FakeApportionmentBasis.Second.UnitId, 10m, 18m, 5m, 5m);

        MonthView view = (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Value;

        Lines(view.Closing!.Charges[0]).Should().Equal(
            (ChargeLineKind.Expenses, "Despesas ordinárias", 600m),
            (ChargeLineKind.Water, "Água", 113.44m),
            (ChargeLineKind.Sewage, "Esgoto", 126.33m),
            (ChargeLineKind.Gas, "Gás", 32.12m));
        Lines(view.Closing.Charges[1]).Should().Equal(
            (ChargeLineKind.Expenses, "Despesas ordinárias", 400m),
            (ChargeLineKind.Water, "Água", 47.43m),
            (ChargeLineKind.Sewage, "Esgoto", 52.63m));
        ChargeLineView water = view.Closing.Charges[0].Lines.Single(line => line.Kind == ChargeLineKind.Water);
        (water.Quantity, water.PreviousReading, water.CurrentReading).Should().Be((17.649m, 1551.913m, 1569.562m));
    }

    [Fact]
    public async Task Close_UsesTheTariffThatAppliesToTheMonth()
    {
        NovemberEntry(1000m);
        ConfigureTariff("2026-01");
        _kit.Tariffs.Existing(ConsumptionTariff.Create(
            _kit.CondominiumId,
            M(2026, 10),
            TariffScale.Create([new TariffBand(null, 100m, 0m)]).Value,
            null,
            100m,
            null,
            FinanceTestKit.Now).Value);
        ReadMeters(FakeApportionmentBasis.First.UnitId, 0m, 5m);
        ReadMeters(FakeApportionmentBasis.Second.UnitId, 0m, 5m);

        MonthView view = (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Value;

        view.Closing!.Charges.Select(charge => charge.Lines.Single(line => line.Kind == ChargeLineKind.Water).Amount).Should().Equal(100m, 100m);
        view.Closing.Charges.SelectMany(charge => charge.Lines).Should().NotContain(line => line.Kind == ChargeLineKind.Sewage);
    }

    [Fact]
    public async Task Close_WithATariffAndAMissingReading_FailsAndClosesNothing()
    {
        NovemberEntry(1000m);
        ConfigureTariff(gas: false);
        ReadMeters(FakeApportionmentBasis.First.UnitId, 0m, 5m);

        Result<MonthView> result = await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default);

        result.Error.Code.Should().Be("closing.readings_pending");
        _kit.Closings.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Close_WithoutATariffTheReadingsAreIgnored()
    {
        NovemberEntry(1000m);
        ReadMeters(FakeApportionmentBasis.First.UnitId, 0m, 5m);

        MonthView view = (await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default)).Value;

        view.Closing!.Charges.SelectMany(charge => charge.Lines).Should().OnlyContain(line => line.Kind == ChargeLineKind.Expenses);
    }

    [Fact]
    public async Task Reopen_KeepsTheReadings()
    {
        NovemberEntry(1000m);
        ConfigureTariff(gas: false);
        ReadMeters(FakeApportionmentBasis.First.UnitId, 0m, 5m);
        ReadMeters(FakeApportionmentBasis.Second.UnitId, 0m, 5m);
        await CloseHandler().Handle(new CloseMonthCommand("2026-11", DueDate, Manager), default);

        await ReopenHandler().Handle(new ReopenMonthCommand("2026-11"), default);

        _kit.Readings.All.Should().HaveCount(2);
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
