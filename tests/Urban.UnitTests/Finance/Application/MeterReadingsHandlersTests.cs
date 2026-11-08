using FluentAssertions;
using Urban.Finance.Application.Consumption;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

public class MeterReadingsHandlersTests
{
    private static readonly Guid First = FakeApportionmentBasis.First.UnitId;
    private static readonly Guid Second = FakeApportionmentBasis.Second.UnitId;

    private readonly FinanceTestKit _kit = new();

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private GetMonthReadingsQueryHandler GetHandler() => new(_kit.Readings, _kit.Tariffs, _kit.Closings, _kit.Basis, _kit.Tenant);

    private SaveMonthReadingsCommandHandler SaveHandler() => new(_kit.Readings, _kit.Tariffs, _kit.Closings, _kit.Basis, _kit.Tenant, _kit.Clock);

    private MeterReading Stored(Guid unit, YearMonth month, ConsumptionService service, decimal previous, decimal current) =>
        _kit.Readings.Existing(MeterReading.Create(_kit.CondominiumId, unit, month, service, previous, current, FinanceTestKit.Now).Value);

    private ConsumptionTariff StoredTariff(string from, bool gas)
    {
        return _kit.Tariffs.Existing(ConsumptionTariff.Create(
            _kit.CondominiumId,
            YearMonth.Parse(from).Value,
            TariffScale.Create([new TariffBand(null, 30m, 5m)]).Value,
            null,
            100m,
            gas ? GasTariff.Create(2.4m, 15.71m).Value : null,
            FinanceTestKit.Now).Value);
    }

    private static SaveMonthReadingsCommand Save(params ReadingInput[] readings) => new("2026-11", readings);

    private static ReadingInput Water(Guid unit, decimal? previous, decimal? current) => new(unit, ConsumptionService.Water, previous, current);

    private static ReadingInput Gas(Guid unit, decimal? previous, decimal? current) => new(unit, ConsumptionService.Gas, previous, current);

    // ---- get ----

    [Fact]
    public async Task Get_ListsTheUnitsInTheRateioOrderWithTheirReadingsOfTheMonth()
    {
        Stored(First, M(2026, 11), ConsumptionService.Water, 1551.913m, 1569.562m);
        Stored(First, M(2026, 11), ConsumptionService.Gas, 117.825m, 118.677m);

        Result<MonthReadingsView> result = await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default);

        result.Value.Month.Should().Be("2026-11");
        result.Value.Units.Select(unit => (unit.UnitId, unit.Block, unit.Number)).Should().Equal((First, null, "101"), (Second, "A", "102"));
        UnitReadingsView first = result.Value.Units[0];
        first.Water.Should().Be(new MeterReadingView(1551.913m, 1569.562m, 17.649m));
        first.Gas.Should().Be(new MeterReadingView(117.825m, 118.677m, 0.852m));
        result.Value.Units[1].Water.Should().BeNull();
        result.Value.Units[1].Gas.Should().BeNull();
    }

    [Fact]
    public async Task Get_SuggestsLastMonthsCurrentReadingAsThePreviousOne()
    {
        Stored(First, M(2026, 10), ConsumptionService.Water, 1500m, 1551.913m);
        Stored(First, M(2026, 10), ConsumptionService.Gas, 100m, 117.825m);
        Stored(Second, M(2026, 10), ConsumptionService.Water, 10m, 12m);

        Result<MonthReadingsView> result = await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default);

        UnitReadingsView first = result.Value.Units[0];
        first.SuggestedWaterPrevious.Should().Be(1551.913m);
        first.SuggestedGasPrevious.Should().Be(117.825m);
        result.Value.Units[1].SuggestedWaterPrevious.Should().Be(12m);
        result.Value.Units[1].SuggestedGasPrevious.Should().BeNull();
    }

    [Fact]
    public async Task Get_IgnoresReadingsOfOtherMonthsAndOtherUnits()
    {
        Stored(First, M(2026, 9), ConsumptionService.Water, 1m, 2m);
        Stored(Guid.CreateVersion7(), M(2026, 11), ConsumptionService.Water, 1m, 2m);

        Result<MonthReadingsView> result = await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default);

        result.Value.Units.Should().OnlyContain(unit => unit.Water == null && unit.Gas == null);
        result.Value.Units.Should().OnlyContain(unit => unit.SuggestedWaterPrevious == null);
    }

    [Fact]
    public async Task Get_SaysWhetherTheMonthIsClosedAndWhichTariffApplies()
    {
        StoredTariff("2026-01", gas: true);
        _kit.NewEntry(_kit.NewFixed(_kit.NewGroup(), M(2026, 9)), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));

        MonthReadingsView view = (await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default)).Value;

        view.IsClosed.Should().BeTrue();
        view.HasTariff.Should().BeTrue();
        view.HasGas.Should().BeTrue();
    }

    [Fact]
    public async Task Get_AMonthBeforeTheFirstTariffHasNoTariffAndAnOpenMonthIsNotClosed()
    {
        StoredTariff("2026-12", gas: true);

        MonthReadingsView view = (await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default)).Value;

        view.IsClosed.Should().BeFalse();
        view.HasTariff.Should().BeFalse();
        view.HasGas.Should().BeFalse();
    }

    [Fact]
    public async Task Get_ATariffWithoutGasHasNoGas()
    {
        StoredTariff("2026-01", gas: false);

        MonthReadingsView view = (await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default)).Value;

        view.HasTariff.Should().BeTrue();
        view.HasGas.Should().BeFalse();
    }

    [Fact]
    public async Task Get_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2026-13")]
    public async Task Get_WithAnInvalidMonth_Fails(string? month)
    {
        (await GetHandler().Handle(new GetMonthReadingsQuery(month), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task Get_WhenTheUnitsAreNotReady_ReturnsThatFailure()
    {
        _kit.Basis.Outcome = Result.Failure<ApportionmentBasis>(new Error("condominium.apportionment_not_ready", "x"));

        (await GetHandler().Handle(new GetMonthReadingsQuery("2026-11"), default)).Error.Code.Should().Be("condominium.apportionment_not_ready");
    }

    // ---- save ----

    [Fact]
    public async Task Save_StoresTheReadingForTheUnitMonthAndServiceAndReturnsTheMonth()
    {
        Result<MonthReadingsView> result = await SaveHandler().Handle(Save(Water(First, 1551.913m, 1569.562m)), default);

        result.IsSuccess.Should().BeTrue();
        MeterReading stored = _kit.Readings.All.Should().ContainSingle().Subject;
        (stored.CondominiumId, stored.UnitId, stored.Month, stored.Service).Should().Be((_kit.CondominiumId, First, M(2026, 11), ConsumptionService.Water));
        (stored.Previous, stored.Current).Should().Be((1551.913m, 1569.562m));
        result.Value.Units[0].Water.Should().Be(new MeterReadingView(1551.913m, 1569.562m, 17.649m));
    }

    [Fact]
    public async Task Save_StoresWaterAndGasOfSeveralUnitsAtOnce()
    {
        await SaveHandler().Handle(Save(Water(First, 10m, 20m), Gas(First, 1m, 2m), Water(Second, 5m, 9m)), default);

        _kit.Readings.All.Select(reading => (reading.UnitId, reading.Service)).Should().BeEquivalentTo(
            [(First, ConsumptionService.Water), (First, ConsumptionService.Gas), (Second, ConsumptionService.Water)]);
        _kit.Readings.Saves.Should().Be(1);
    }

    [Fact]
    public async Task Save_WithoutThePreviousReading_UsesLastMonthsCurrentOne()
    {
        Stored(First, M(2026, 10), ConsumptionService.Water, 1500m, 1551.913m);

        await SaveHandler().Handle(Save(Water(First, null, 1569.562m)), default);

        _kit.Readings.All.Single(reading => reading.Month == M(2026, 11)).Previous.Should().Be(1551.913m);
    }

    [Fact]
    public async Task Save_WithoutThePreviousReadingAndNoLastMonth_StartsFromZero()
    {
        await SaveHandler().Handle(Save(Water(First, null, 12.5m)), default);

        _kit.Readings.All.Single().Previous.Should().Be(0m);
    }

    [Fact]
    public async Task Save_AReadingThatAlreadyExists_CorrectsTheSameOne()
    {
        MeterReading existing = Stored(First, M(2026, 11), ConsumptionService.Water, 10m, 20m);

        await SaveHandler().Handle(Save(Water(First, 10m, 25m)), default);

        _kit.Readings.All.Should().ContainSingle().Which.Should().BeSameAs(existing);
        existing.Current.Should().Be(25m);
    }

    [Fact]
    public async Task Save_AReadingThatAlreadyExistsWithoutThePrevious_KeepsItsPrevious()
    {
        MeterReading existing = Stored(First, M(2026, 11), ConsumptionService.Water, 10m, 20m);

        await SaveHandler().Handle(Save(Water(First, null, 30m)), default);

        (existing.Previous, existing.Current).Should().Be((10m, 30m));
    }

    [Fact]
    public async Task Save_WithoutTheCurrentReading_RemovesTheReading()
    {
        Stored(First, M(2026, 11), ConsumptionService.Water, 10m, 20m);
        Stored(First, M(2026, 11), ConsumptionService.Gas, 1m, 2m);

        Result<MonthReadingsView> result = await SaveHandler().Handle(Save(Water(First, null, null)), default);

        _kit.Readings.All.Should().ContainSingle().Which.Service.Should().Be(ConsumptionService.Gas);
        result.Value.Units[0].Water.Should().BeNull();
    }

    [Fact]
    public async Task Save_RemovingAReadingThatDoesNotExist_ChangesNothing()
    {
        Result<MonthReadingsView> result = await SaveHandler().Handle(Save(Water(First, null, null)), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Readings.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_TheSameUnitAndServiceTwice_KeepsTheLastOne()
    {
        await SaveHandler().Handle(Save(Water(First, 10m, 20m), Water(First, 10m, 22m)), default);

        _kit.Readings.All.Should().ContainSingle().Which.Current.Should().Be(22m);
    }

    [Fact]
    public async Task Save_WithoutReadings_ChangesNothingAndReturnsTheMonth()
    {
        Result<MonthReadingsView> result = await SaveHandler().Handle(new SaveMonthReadingsCommand("2026-11", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Units.Should().HaveCount(2);
    }

    [Fact]
    public async Task Save_WithACurrentReadingBelowThePrevious_FailsAndSavesNothingFromTheRequest()
    {
        Result<MonthReadingsView> result = await SaveHandler().Handle(Save(Water(First, 10m, 20m), Water(Second, 50m, 40m)), default);

        result.Error.Should().Be(ConsumptionErrors.ReadingInvalid);
        _kit.Readings.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_ACorrectionThatIsInvalid_LeavesTheExistingReadingAsItWas()
    {
        MeterReading existing = Stored(First, M(2026, 11), ConsumptionService.Water, 10m, 20m);

        (await SaveHandler().Handle(Save(Water(First, 10m, 5m)), default)).Error.Should().Be(ConsumptionErrors.ReadingInvalid);

        existing.Current.Should().Be(20m);
    }

    [Fact]
    public async Task Save_ForAUnitThatIsNotInTheCondominium_Fails()
    {
        (await SaveHandler().Handle(Save(Water(Guid.CreateVersion7(), 1m, 2m)), default)).Error.Should().Be(ConsumptionErrors.UnitNotFound);
        (await SaveHandler().Handle(Save(Water(Guid.Empty, 1m, 2m)), default)).Error.Should().Be(ConsumptionErrors.UnitNotFound);
    }

    [Fact]
    public async Task Save_WithAnUnknownService_Fails()
    {
        (await SaveHandler().Handle(Save(new ReadingInput(First, (ConsumptionService)(-1), 1m, 2m)), default)).Error.Should().Be(ConsumptionErrors.ServiceInvalid);
    }

    [Fact]
    public async Task Save_InAClosedMonth_FailsAndChangesNothing()
    {
        _kit.NewEntry(_kit.NewFixed(_kit.NewGroup(), M(2026, 9)), M(2026, 11));
        _kit.CloseMonth(M(2026, 11));

        Result<MonthReadingsView> result = await SaveHandler().Handle(Save(Water(First, 1m, 2m)), default);

        result.Error.Should().Be(ClosingErrors.MonthIsClosed);
        _kit.Readings.All.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2026-13")]
    public async Task Save_WithAnInvalidMonth_Fails(string? month)
    {
        (await SaveHandler().Handle(new SaveMonthReadingsCommand(month, [Water(First, 1m, 2m)]), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task Save_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await SaveHandler().Handle(Save(Water(First, 1m, 2m)), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Save_WhenTheUnitsAreNotReady_ReturnsThatFailure()
    {
        _kit.Basis.Outcome = Result.Failure<ApportionmentBasis>(new Error("condominium.apportionment_not_ready", "x"));

        (await SaveHandler().Handle(Save(Water(First, 1m, 2m)), default)).Error.Code.Should().Be("condominium.apportionment_not_ready");
    }

    [Fact]
    public async Task Save_WhenTheStoreFails_ReturnsTheFailure()
    {
        _kit.Readings.SaveFailure = FinanceErrors.ConcurrencyConflict;

        (await SaveHandler().Handle(Save(Water(First, 1m, 2m)), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
    }
}
