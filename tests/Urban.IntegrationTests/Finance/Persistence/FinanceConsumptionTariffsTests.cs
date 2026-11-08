using FluentAssertions;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceConsumptionTariffsTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    private ConsumptionTariff Tariff(YearMonth from, decimal sewagePercent = 100m, bool sewage = true, bool gas = true, decimal fixedWater = 47.43m) =>
        ConsumptionTariff.Create(
            _kit.CondominiumId,
            from,
            TariffScale.Create([new TariffBand(10m, fixedWater, 0m), new TariffBand(30m, 0m, 8.63m), new TariffBand(null, 0m, 11m)]).Value,
            sewage ? TariffScale.Create([new TariffBand(10m, 52.63m, 0m), new TariffBand(null, 0m, 9.635m)]).Value : null,
            sewagePercent,
            gas ? GasTariff.Create(2.4m, 15.71m).Value : null,
            FinanceStoreKit.Now).Value;

    [Fact]
    public async Task Save_ThenFind_ReturnsTheBandsTheSewageAndTheGasExactly()
    {
        ConsumptionTariff tariff = Tariff(FinanceStoreKit.M(2026, 1), sewagePercent: 50m);

        (await _kit.Tariffs().SaveAsync(tariff, default)).IsSuccess.Should().BeTrue();
        ConsumptionTariff found = (await _kit.Tariffs().FindAsync(FinanceStoreKit.M(2026, 1), default))!;

        found.Id.Should().Be(tariff.Id);
        found.CondominiumId.Should().Be(_kit.CondominiumId);
        found.ValidFrom.Should().Be(FinanceStoreKit.M(2026, 1));
        found.CreatedAt.Should().Be(FinanceStoreKit.Now);
        found.Water.Bands.Should().Equal(tariff.Water.Bands);
        found.Sewage!.Bands.Should().Equal(tariff.Sewage!.Bands);
        found.SewagePercent.Should().Be(50m);
        found.Gas!.KilosPerCubicMeter.Should().Be(2.4m);
        found.Gas.PricePerKilo.Should().Be(15.71m);
        found.Gas.PricePerCubicMeter.Should().Be(37.70m);
    }

    [Fact]
    public async Task ATariffReadBack_CalculatesLikeTheOneThatWasSaved()
    {
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 1)), default);

        ConsumptionTariff found = (await _kit.Tariffs().FindAsync(FinanceStoreKit.M(2026, 1), default))!;

        found.WaterCharge(17.649m).Should().Be(113.44m);
        found.SewageCharge(17.649m).Should().Be(126.33m);
        found.GasCharge(0.852m).Should().Be(32.12m);
    }

    [Fact]
    public async Task Save_WithoutSewageAndGas_ReadsBackWithoutThem()
    {
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 1), sewage: false, gas: false), default);

        ConsumptionTariff found = (await _kit.Tariffs().FindAsync(FinanceStoreKit.M(2026, 1), default))!;

        found.Sewage.Should().BeNull();
        found.Gas.Should().BeNull();
    }

    [Fact]
    public async Task Save_ForTheSameMonth_ReplacesTheTariff()
    {
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 1), fixedWater: 40m), default);

        Result replaced = await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 1), fixedWater: 50m), default);

        replaced.IsSuccess.Should().BeTrue();
        IReadOnlyList<ConsumptionTariff> all = await _kit.Tariffs().ListAsync(default);
        all.Should().ContainSingle().Which.Water.Bands[0].FixedAmount.Should().Be(50m);
    }

    [Fact]
    public async Task List_ReturnsTheTariffsOfThisCondominiumNewestFirst()
    {
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2025, 1)), default);
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 3)), default);
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2025, 9)), default);

        IReadOnlyList<ConsumptionTariff> all = await _kit.Tariffs().ListAsync(default);

        all.Select(tariff => tariff.ValidFrom).Should().Equal(FinanceStoreKit.M(2026, 3), FinanceStoreKit.M(2025, 9), FinanceStoreKit.M(2025, 1));
        (await _kit.Tariffs(Guid.CreateVersion7()).ListAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Find_ForAnotherCondominiumOrAnotherMonth_ReturnsNull()
    {
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 1)), default);

        (await _kit.Tariffs(Guid.CreateVersion7()).FindAsync(FinanceStoreKit.M(2026, 1), default)).Should().BeNull();
        (await _kit.Tariffs().FindAsync(FinanceStoreKit.M(2026, 2), default)).Should().BeNull();
    }

    [Fact]
    public async Task TheSameMonthInTwoCondominiums_IsAllowed()
    {
        Guid other = Guid.CreateVersion7();
        ConsumptionTariff foreign = ConsumptionTariff.Create(other, FinanceStoreKit.M(2026, 1), TariffScale.Create([new TariffBand(null, 10m, 1m)]).Value, null, 100m, null, FinanceStoreKit.Now).Value;

        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 1)), default);
        await _kit.Tariffs(other).SaveAsync(foreign, default);

        (await _kit.Tariffs(other).FindAsync(FinanceStoreKit.M(2026, 1), default))!.Water.Bands.Should().ContainSingle();
        (await _kit.Tariffs().FindAsync(FinanceStoreKit.M(2026, 1), default))!.Water.Bands.Should().HaveCount(3);
    }

    [Fact]
    public async Task Remove_DeletesOnlyThatTariff()
    {
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2025, 1)), default);
        await _kit.Tariffs().SaveAsync(Tariff(FinanceStoreKit.M(2026, 1)), default);
        FinanceConsumptionTariffs store = _kit.Tariffs();

        await store.RemoveAsync((await store.FindAsync(FinanceStoreKit.M(2026, 1), default))!, default);

        (await _kit.Tariffs().ListAsync(default)).Select(tariff => tariff.ValidFrom).Should().Equal(FinanceStoreKit.M(2025, 1));
    }
}
