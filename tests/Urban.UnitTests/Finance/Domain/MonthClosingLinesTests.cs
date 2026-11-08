using FluentAssertions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Domain;

public class MonthClosingLinesTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid Ordinary = Guid.CreateVersion7();
    private static readonly Guid Garages = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly YearMonth September = YearMonth.Create(2026, 9).Value;
    private static readonly YearMonth August = YearMonth.Create(2026, 8).Value;
    private static readonly DateOnly DueDate = new(2026, 10, 10);

    private static readonly ApportionmentUnit UnitA = new(Guid.CreateVersion7(), null, "301", 0.6m);
    private static readonly ApportionmentUnit UnitB = new(Guid.CreateVersion7(), null, "302", 0.4m);

    private static ApportionmentBasis Basis(params ApportionmentUnit[] units) => new(ApportionmentBasisKind.IdealFraction, units);

    private static ExpenseEntry Fixed(Guid groupId, decimal amount, string description) =>
        Expense.CreateFixed(CondominiumId, groupId, description, null, September, null, amount, 10, Now).Value.GenerateEntry(September, Now).Value;

    private static ExpenseEntry[] Entries() => [Fixed(Ordinary, 700m, "Zeladora"), Fixed(Ordinary, 300m, "Limpeza"), Fixed(Garages, 300m, "Benedett")];

    private static ConsumptionTariff Tariff(bool sewage = true, bool gas = true, decimal sewagePercent = 100m) =>
        ConsumptionTariff.Create(
            CondominiumId,
            YearMonth.Create(2026, 1).Value,
            TariffScale.Create([new TariffBand(10m, 47.43m, 0m), new TariffBand(30m, 0m, 8.63m), new TariffBand(null, 0m, 11m)]).Value,
            sewage ? TariffScale.Create([new TariffBand(10m, 52.63m, 0m), new TariffBand(30m, 0m, 9.635m), new TariffBand(null, 0m, 12.279m)]).Value : null,
            sewagePercent,
            gas ? GasTariff.Create(2.4m, 15.71m).Value : null,
            Now).Value;

    private static MeterReading Reading(ApportionmentUnit unit, ConsumptionService service, decimal previous, decimal current, YearMonth? month = null) =>
        MeterReading.Create(CondominiumId, unit.UnitId, month ?? September, service, previous, current, Now).Value;

    private static MeterReading[] AllReadings() =>
    [
        Reading(UnitA, ConsumptionService.Water, 1551.913m, 1569.562m),
        Reading(UnitA, ConsumptionService.Gas, 117.825m, 118.677m),
        Reading(UnitB, ConsumptionService.Water, 10m, 18m),
        Reading(UnitB, ConsumptionService.Gas, 5m, 5m),
    ];

    private static ClosingOptions Options(
        decimal reservePercent = 15m,
        ConsumptionTariff? tariff = null,
        IReadOnlyList<MeterReading>? readings = null,
        IReadOnlySet<Guid>? reserveGroups = null) =>
        new(
            new Dictionary<Guid, string> { [Ordinary] = "Despesas ordinárias", [Garages] = "Manutenção das garagens" },
            reserveGroups ?? new HashSet<Guid> { Ordinary },
            reservePercent,
            tariff,
            readings ?? []);

    private static Result<MonthClosing> Close(ClosingOptions? options, IReadOnlyList<ExpenseEntry>? entries = null, ApportionmentBasis? basis = null) =>
        MonthClosing.Close(CondominiumId, September, DueDate, Manager, Now, entries ?? Entries(), basis ?? Basis(UnitA, UnitB), options);

    private static List<(ChargeLineKind Kind, string Label, decimal Amount)> Lines(UnitCharge charge) =>
        [.. charge.Lines.Select(line => (line.Kind, line.Label, line.Amount))];

    [Fact]
    public void Close_BuildsOneLinePerGroupTheReserveFundAndTheConsumptionOfEachUnit()
    {
        MonthClosing closing = Close(Options(tariff: Tariff(), readings: AllReadings())).Value;

        Lines(closing.Charges[0]).Should().Equal(
            (ChargeLineKind.Expenses, "Despesas ordinárias", 600m),
            (ChargeLineKind.Expenses, "Manutenção das garagens", 180m),
            (ChargeLineKind.ReserveFund, "Fundo de reserva", 90m),
            (ChargeLineKind.Water, "Água", 113.44m),
            (ChargeLineKind.Sewage, "Esgoto", 126.33m),
            (ChargeLineKind.Gas, "Gás", 32.12m));
        Lines(closing.Charges[1]).Should().Equal(
            (ChargeLineKind.Expenses, "Despesas ordinárias", 400m),
            (ChargeLineKind.Expenses, "Manutenção das garagens", 120m),
            (ChargeLineKind.ReserveFund, "Fundo de reserva", 60m),
            (ChargeLineKind.Water, "Água", 47.43m),
            (ChargeLineKind.Sewage, "Esgoto", 52.63m));
    }

    [Fact]
    public void Close_ConsumptionLinesCarryTheQuantityAndTheReadings()
    {
        UnitChargeLine water = Close(Options(tariff: Tariff(), readings: AllReadings())).Value.Charges[0].Lines.Single(line => line.Kind == ChargeLineKind.Water);

        water.Quantity.Should().Be(17.649m);
        water.PreviousReading.Should().Be(1551.913m);
        water.CurrentReading.Should().Be(1569.562m);
    }

    [Fact]
    public void Close_ExpenseAndReserveLinesHaveNoQuantityOrReadings()
    {
        UnitChargeLine line = Close(Options(tariff: Tariff(), readings: AllReadings())).Value.Charges[0].Lines.First();

        line.Quantity.Should().BeNull();
        line.PreviousReading.Should().BeNull();
        line.CurrentReading.Should().BeNull();
    }

    [Fact]
    public void Close_TheChargeIsTheSumOfItsLinesAndTheTotalIsStillTheExpenses()
    {
        MonthClosing closing = Close(Options(tariff: Tariff(), readings: AllReadings())).Value;

        closing.Total.Should().Be(1300m);
        closing.Charges[0].Amount.Should().Be(1141.89m);
        closing.Charges[1].Amount.Should().Be(680.06m);
        closing.Charges.Should().OnlyContain(charge => charge.Amount == charge.Lines.Sum(line => line.Amount));
    }

    [Fact]
    public void Close_EachGroupIsSplitOnItsOwnAndAddsUpExactly()
    {
        ApportionmentBasis thirds = new(ApportionmentBasisKind.EqualShare, [UnitA with { Weight = 1m }, UnitB with { Weight = 1m }, new(Guid.CreateVersion7(), null, "303", 1m)]);

        MonthClosing closing = Close(Options(reservePercent: 0m), [Fixed(Ordinary, 100m, "A"), Fixed(Garages, 100m, "B")], thirds).Value;

        closing.Charges.Select(charge => charge.Lines.Select(line => line.Amount).ToArray()).Should().BeEquivalentTo(
            new[] { new[] { 33.34m, 33.34m }, new[] { 33.33m, 33.33m }, new[] { 33.33m, 33.33m } },
            options => options.WithStrictOrdering());
        closing.Charges.Sum(charge => charge.Amount).Should().Be(200m);
    }

    [Fact]
    public void Close_TheReserveFundIsThePercentageOfTheMarkedGroupsOnly()
    {
        MonthClosing both = Close(Options(reservePercent: 10m, reserveGroups: new HashSet<Guid> { Ordinary, Garages })).Value;
        MonthClosing garagesOnly = Close(Options(reservePercent: 10m, reserveGroups: new HashSet<Guid> { Garages })).Value;

        both.Charges.Sum(charge => charge.Lines.Where(line => line.Kind == ChargeLineKind.ReserveFund).Sum(line => line.Amount)).Should().Be(130m);
        garagesOnly.Charges.Sum(charge => charge.Lines.Where(line => line.Kind == ChargeLineKind.ReserveFund).Sum(line => line.Amount)).Should().Be(30m);
    }

    [Fact]
    public void Close_WithoutAReservePercentageOrMarkedGroups_HasNoReserveLine()
    {
        Close(Options(reservePercent: 0m)).Value.Charges.SelectMany(charge => charge.Lines).Should().NotContain(line => line.Kind == ChargeLineKind.ReserveFund);
        Close(Options(reserveGroups: new HashSet<Guid>())).Value.Charges.SelectMany(charge => charge.Lines).Should().NotContain(line => line.Kind == ChargeLineKind.ReserveFund);
    }

    [Fact]
    public void Close_CanceledEntriesLeaveTheGroupLinesAndTheReserveBase()
    {
        ExpenseEntry canceled = Fixed(Ordinary, 5000m, "Obra");
        canceled.SetStatus(EntryStatus.Canceled);

        MonthClosing closing = Close(Options(), [.. Entries(), canceled]).Value;

        closing.Total.Should().Be(1300m);
        Lines(closing.Charges[0]).Should().Contain((ChargeLineKind.ReserveFund, "Fundo de reserva", 90m));
    }

    [Fact]
    public void Close_GroupLinesComeInAlphabeticalOrderWhateverTheOrderOfTheEntries()
    {
        MonthClosing closing = Close(Options(reservePercent: 0m), [Fixed(Garages, 300m, "Benedett"), Fixed(Ordinary, 700m, "Zeladora")]).Value;

        closing.Charges[0].Lines.Select(line => line.Label).Should().Equal("Despesas ordinárias", "Manutenção das garagens");
    }

    [Fact]
    public void Close_AGroupWithoutAnyAmountGetsNoLine()
    {
        ExpenseEntry canceled = Fixed(Garages, 300m, "Benedett");
        canceled.SetStatus(EntryStatus.Canceled);

        MonthClosing closing = Close(Options(), [Fixed(Ordinary, 1000m, "Zeladora"), canceled]).Value;

        closing.Charges[0].Lines.Select(line => line.Label).Should().Equal("Despesas ordinárias", "Fundo de reserva");
    }

    [Fact]
    public void Close_ASewagePercentageOfZeroLeavesTheSewageLineOut()
    {
        MonthClosing closing = Close(Options(tariff: Tariff(sewagePercent: 0m), readings: AllReadings())).Value;

        closing.Charges.SelectMany(charge => charge.Lines).Should().NotContain(line => line.Kind == ChargeLineKind.Sewage);
    }

    [Fact]
    public void Close_ATariffWithoutSewageOrGasHasOnlyTheWaterLine()
    {
        MonthClosing closing = Close(Options(tariff: Tariff(sewage: false, gas: false), readings: [Reading(UnitA, ConsumptionService.Water, 1m, 5m), Reading(UnitB, ConsumptionService.Water, 1m, 5m)])).Value;

        closing.Charges.SelectMany(charge => charge.Lines).Select(line => line.Kind).Distinct().Should().Equal(ChargeLineKind.Expenses, ChargeLineKind.ReserveFund, ChargeLineKind.Water);
    }

    [Fact]
    public void Close_ZeroGasConsumptionHasNoGasLine()
    {
        MonthClosing closing = Close(Options(tariff: Tariff(), readings: AllReadings())).Value;

        closing.Charges[1].Lines.Should().NotContain(line => line.Kind == ChargeLineKind.Gas);
        closing.Charges[0].Lines.Should().Contain(line => line.Kind == ChargeLineKind.Gas);
    }

    [Fact]
    public void Close_WithATariffEveryUnitNeedsAWaterReading()
    {
        MeterReading[] missingB = [AllReadings()[0], AllReadings()[1]];

        Result<MonthClosing> result = Close(Options(tariff: Tariff(), readings: missingB));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("closing.readings_pending");
        result.Error.Description.Should().Contain("água").And.Contain("1 unidade");
    }

    [Fact]
    public void Close_WithGasInTheTariffEveryUnitNeedsAGasReading()
    {
        MeterReading[] noGas = [AllReadings()[0], AllReadings()[2]];

        Result<MonthClosing> result = Close(Options(tariff: Tariff(), readings: noGas));

        result.Error.Code.Should().Be("closing.readings_pending");
        result.Error.Description.Should().Contain("gás").And.Contain("2 unidades");
    }

    [Fact]
    public void Close_WithoutGasInTheTariffGasReadingsAreNotNeeded()
    {
        Close(Options(tariff: Tariff(gas: false), readings: [AllReadings()[0], AllReadings()[2]])).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Close_IgnoresReadingsOfAnotherMonth()
    {
        MeterReading[] old = [Reading(UnitA, ConsumptionService.Water, 1m, 2m, August), Reading(UnitB, ConsumptionService.Water, 1m, 2m, August)];

        Close(Options(tariff: Tariff(sewage: false, gas: false), readings: old)).Error.Code.Should().Be("closing.readings_pending");
    }

    [Fact]
    public void Close_WithoutATariffReadingsAreNotNeededAndThereIsNoConsumptionLine()
    {
        MonthClosing closing = Close(Options(tariff: null)).Value;

        closing.Charges.SelectMany(charge => charge.Lines).Select(line => line.Kind).Should().OnlyContain(kind => kind == ChargeLineKind.Expenses || kind == ChargeLineKind.ReserveFund);
    }

    [Fact]
    public void Close_WithoutOptions_BuildsOneExpenseLinePerGroupWithAGenericLabel()
    {
        MonthClosing closing = Close(null).Value;

        closing.Charges[0].Lines.Should().HaveCount(2);
        closing.Charges[0].Lines.Should().OnlyContain(line => line.Kind == ChargeLineKind.Expenses && line.Label == "Despesas");
        closing.Charges.Sum(charge => charge.Amount).Should().Be(1300m);
    }

    [Fact]
    public void Close_LinesKeepTheirOrderWithPositionsAndTheChargeId()
    {
        UnitCharge charge = Close(Options(tariff: Tariff(), readings: AllReadings())).Value.Charges[0];

        charge.Lines.Select(line => line.Position).Should().Equal(0, 1, 2, 3, 4, 5);
        charge.Lines.Should().OnlyContain(line => line.ChargeId == charge.Id && line.CondominiumId == CondominiumId);
    }
}
