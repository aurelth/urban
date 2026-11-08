using FluentAssertions;
using Urban.Finance.Application.Consumption;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class ConsumptionTariffHandlersTests
{
    private readonly FinanceTestKit _kit = new();

    private ListConsumptionTariffsQueryHandler ListHandler() => new(_kit.Tariffs, _kit.Tenant);

    private SaveConsumptionTariffCommandHandler SaveHandler() => new(_kit.Tariffs, _kit.Tenant, _kit.Clock);

    private DeleteConsumptionTariffCommandHandler DeleteHandler() => new(_kit.Tariffs, _kit.Tenant);

    private static IReadOnlyList<TariffBandInput> WaterBands() =>
        [new(10m, 47.43m, 0m), new(30m, 0m, 8.63m), new(null, 0m, 11m)];

    private static IReadOnlyList<TariffBandInput> SewageBands() =>
        [new(10m, 52.63m, 0m), new(null, 0m, 9.635m)];

    private static SaveConsumptionTariffCommand Command(
        string month = "2026-01",
        IReadOnlyList<TariffBandInput>? water = null,
        IReadOnlyList<TariffBandInput>? sewage = null,
        decimal sewagePercent = 100m,
        GasTariffInput? gas = null) =>
        new(month, water ?? WaterBands(), sewage, sewagePercent, gas);

    private ConsumptionTariff Stored(string month, decimal sewagePercent = 100m) =>
        _kit.Tariffs.Existing(ConsumptionTariff.Create(
            _kit.CondominiumId,
            YearMonth.Parse(month).Value,
            TariffScale.Create([new TariffBand(null, 30m, 5m)]).Value,
            null,
            sewagePercent,
            null,
            FinanceTestKit.Now).Value);

    // ---- list ----

    [Fact]
    public async Task List_ReturnsTheTariffsNewestFirst()
    {
        Stored("2025-01");
        Stored("2026-03");
        Stored("2025-09");

        Result<IReadOnlyList<ConsumptionTariffView>> result = await ListHandler().Handle(new ListConsumptionTariffsQuery(), default);

        result.Value.Select(view => view.ValidFrom).Should().Equal("2026-03", "2025-09", "2025-01");
    }

    [Fact]
    public async Task List_WithoutTariffs_ReturnsAnEmptyList()
    {
        (await ListHandler().Handle(new ListConsumptionTariffsQuery(), default)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await ListHandler().Handle(new ListConsumptionTariffsQuery(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- save ----

    [Fact]
    public async Task Save_WithOnlyWater_StoresTheBandsAndNoSewageOrGas()
    {
        Result<ConsumptionTariffView> result = await SaveHandler().Handle(Command(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ValidFrom.Should().Be("2026-01");
        result.Value.Water.Should().Equal(new TariffBandView(10m, 47.43m, 0m), new TariffBandView(30m, 0m, 8.63m), new TariffBandView(null, 0m, 11m));
        result.Value.Sewage.Should().BeNull();
        result.Value.Gas.Should().BeNull();
        _kit.Tariffs.All.Should().ContainSingle().Which.CondominiumId.Should().Be(_kit.CondominiumId);
    }

    [Fact]
    public async Task Save_WithSewageAndGas_ReturnsThemWithThePercentageAndThePricePerCubicMeter()
    {
        Result<ConsumptionTariffView> result = await SaveHandler().Handle(Command(sewage: SewageBands(), sewagePercent: 50m, gas: new GasTariffInput(2.4m, 15.71m)), default);

        result.Value.Sewage.Should().Equal(new TariffBandView(10m, 52.63m, 0m), new TariffBandView(null, 0m, 9.635m));
        result.Value.SewagePercent.Should().Be(50m);
        result.Value.Gas.Should().Be(new GasTariffView(2.4m, 15.71m, 37.70m));
    }

    [Fact]
    public async Task Save_AnEmptySewageListMeansNoSewage()
    {
        (await SaveHandler().Handle(Command(sewage: []), default)).Value.Sewage.Should().BeNull();
    }

    [Fact]
    public async Task Save_StoresTariffsThatCalculateLikeTheSpreadsheet()
    {
        await SaveHandler().Handle(Command(sewage: SewageBands(), gas: new GasTariffInput(2.4m, 15.71m)), default);

        ConsumptionTariff tariff = _kit.Tariffs.All.Single();

        tariff.WaterCharge(17.649m).Should().Be(113.44m);
        tariff.GasCharge(0.852m).Should().Be(32.12m);
    }

    [Fact]
    public async Task Save_ForAMonthThatAlreadyHasATariff_ReplacesIt()
    {
        Stored("2026-01");

        await SaveHandler().Handle(Command(month: "2026-01"), default);

        _kit.Tariffs.All.Should().ContainSingle().Which.Water.Bands.Should().HaveCount(3);
    }

    [Fact]
    public async Task Save_ForANewMonth_KeepsTheOldTariffs()
    {
        Stored("2025-01");

        await SaveHandler().Handle(Command(month: "2026-01"), default);

        _kit.Tariffs.All.Select(tariff => tariff.ValidFrom.ToString()).Should().BeEquivalentTo("2025-01", "2026-01");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-13")]
    [InlineData("janeiro")]
    public async Task Save_WithAnInvalidMonth_Fails(string? month)
    {
        (await SaveHandler().Handle(Command(month: month!), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task Save_WithoutWaterBands_Fails()
    {
        (await SaveHandler().Handle(new SaveConsumptionTariffCommand("2026-01", null, null, 100m, null), default)).Error.Should().Be(ConsumptionErrors.BandsInvalid);
        (await SaveHandler().Handle(Command(water: []), default)).Error.Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Fact]
    public async Task Save_WithBadWaterBands_Fails()
    {
        (await SaveHandler().Handle(Command(water: [new(30m, 0m, 8.63m), new(10m, 0m, 11m), new(null, 0m, 11m)]), default)).Error.Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Fact]
    public async Task Save_WithBadSewageBands_Fails()
    {
        (await SaveHandler().Handle(Command(sewage: [new(10m, 52.63m, 0m)]), default)).Error.Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Fact]
    public async Task Save_WithABadGasTariff_Fails()
    {
        (await SaveHandler().Handle(Command(gas: new GasTariffInput(0m, 15.71m)), default)).Error.Should().Be(ConsumptionErrors.GasInvalid);
    }

    [Fact]
    public async Task Save_WithABadSewagePercentage_Fails()
    {
        (await SaveHandler().Handle(Command(sewage: SewageBands(), sewagePercent: 101m), default)).Error.Should().Be(ConsumptionErrors.SewagePercentInvalid);
    }

    [Fact]
    public async Task Save_WhenSomethingIsInvalid_StoresNothing()
    {
        Stored("2026-01");

        await SaveHandler().Handle(Command(month: "2026-01", gas: new GasTariffInput(0m, 15.71m)), default);

        _kit.Tariffs.All.Should().ContainSingle().Which.Water.Bands.Should().HaveCount(1);
    }

    [Fact]
    public async Task Save_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await SaveHandler().Handle(Command(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Save_WhenTheStoreFails_ReturnsTheFailure()
    {
        _kit.Tariffs.SaveFailure = FinanceErrors.ConcurrencyConflict;

        (await SaveHandler().Handle(Command(), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_RemovesTheTariffOfThatMonthOnly()
    {
        Stored("2025-01");
        Stored("2026-01");

        Result result = await DeleteHandler().Handle(new DeleteConsumptionTariffCommand("2026-01"), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Tariffs.All.Should().ContainSingle().Which.ValidFrom.ToString().Should().Be("2025-01");
    }

    [Fact]
    public async Task Delete_ForAMonthWithoutATariff_Fails()
    {
        (await DeleteHandler().Handle(new DeleteConsumptionTariffCommand("2026-01"), default)).Error.Should().Be(ConsumptionErrors.TariffNotFound);
    }

    [Fact]
    public async Task Delete_WithAnInvalidMonth_Fails()
    {
        (await DeleteHandler().Handle(new DeleteConsumptionTariffCommand("2026-13"), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task Delete_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await DeleteHandler().Handle(new DeleteConsumptionTariffCommand("2026-01"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }
}
