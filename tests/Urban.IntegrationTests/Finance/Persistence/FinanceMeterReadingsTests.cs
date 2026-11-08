using FluentAssertions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceMeterReadingsTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    private MeterReading Reading(Guid unit, YearMonth month, ConsumptionService service, decimal previous, decimal current, Guid? condominium = null) =>
        MeterReading.Create(condominium ?? _kit.CondominiumId, unit, month, service, previous, current, FinanceStoreKit.Now).Value;

    [Fact]
    public async Task Save_ThenList_ReturnsTheReadingsOfTheMonthWithThreeDecimals()
    {
        Guid unit = Guid.CreateVersion7();
        MeterReading water = Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 1551.913m, 1569.562m);
        MeterReading gas = Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Gas, 117.825m, 118.677m);

        (await _kit.Readings().SaveAsync([water, gas], [], default)).IsSuccess.Should().BeTrue();
        IReadOnlyList<MeterReading> found = await _kit.Readings().ListAsync(FinanceStoreKit.M(2026, 11), default);

        found.Select(reading => reading.Id).Should().BeEquivalentTo([water.Id, gas.Id]);
        MeterReading back = found.Single(reading => reading.Service == ConsumptionService.Water);
        (back.CondominiumId, back.UnitId, back.Month, back.CreatedAt).Should().Be((_kit.CondominiumId, unit, FinanceStoreKit.M(2026, 11), FinanceStoreKit.Now));
        (back.Previous, back.Current, back.Consumption).Should().Be((1551.913m, 1569.562m, 17.649m));
    }

    [Fact]
    public async Task List_ReturnsOnlyTheRequestedMonth()
    {
        Guid unit = Guid.CreateVersion7();
        await _kit.Readings().SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 10), ConsumptionService.Water, 1m, 2m), Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 2m, 3m)], [], default);

        IReadOnlyList<MeterReading> found = await _kit.Readings().ListAsync(FinanceStoreKit.M(2026, 11), default);

        found.Should().ContainSingle().Which.Current.Should().Be(3m);
    }

    [Fact]
    public async Task A_correction_made_on_a_listed_reading_is_saved()
    {
        Guid unit = Guid.CreateVersion7();
        await _kit.Readings().SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 10m, 20m)], [], default);
        FinanceMeterReadings store = _kit.Readings();
        MeterReading listed = (await store.ListAsync(FinanceStoreKit.M(2026, 11), default)).Single();

        listed.Correct(10m, 25.5m);
        (await store.SaveAsync([], [], default)).IsSuccess.Should().BeTrue();

        (await _kit.Readings().ListAsync(FinanceStoreKit.M(2026, 11), default)).Single().Current.Should().Be(25.5m);
    }

    [Fact]
    public async Task Save_RemovesTheRemovedReadingsAndAddsTheNewOnesInOneOperation()
    {
        Guid unit = Guid.CreateVersion7();
        await _kit.Readings().SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 10m, 20m)], [], default);
        FinanceMeterReadings store = _kit.Readings();
        MeterReading old = (await store.ListAsync(FinanceStoreKit.M(2026, 11), default)).Single();

        (await store.SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Gas, 1m, 2m)], [old], default)).IsSuccess.Should().BeTrue();

        (await _kit.Readings().ListAsync(FinanceStoreKit.M(2026, 11), default)).Single().Service.Should().Be(ConsumptionService.Gas);
    }

    [Fact]
    public async Task Save_ASecondReadingOfTheSameUnitMonthAndService_IsAConcurrencyConflict()
    {
        Guid unit = Guid.CreateVersion7();
        await _kit.Readings().SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 10m, 20m)], [], default);

        Result result = await _kit.Readings().SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 10m, 30m)], [], default);

        result.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
    }

    [Fact]
    public async Task TheSameUnitMonthAndServiceCanExistInTwoCondominiums_AndEachOneSeesOnlyItsOwn()
    {
        Guid unit = Guid.CreateVersion7();
        Guid other = Guid.CreateVersion7();
        await _kit.Readings().SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 10m, 20m)], [], default);
        await _kit.Readings(other).SaveAsync([Reading(unit, FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 1m, 2m, other)], [], default);

        (await _kit.Readings().ListAsync(FinanceStoreKit.M(2026, 11), default)).Single().Current.Should().Be(20m);
        (await _kit.Readings(other).ListAsync(FinanceStoreKit.M(2026, 11), default)).Single().Current.Should().Be(2m);
    }

    [Fact]
    public async Task WithoutATenant_NothingIsVisible()
    {
        await _kit.Readings().SaveAsync([Reading(Guid.CreateVersion7(), FinanceStoreKit.M(2026, 11), ConsumptionService.Water, 10m, 20m)], [], default);

        (await new FinanceMeterReadings(postgres.CreateContext(null)).ListAsync(FinanceStoreKit.M(2026, 11), default)).Should().BeEmpty();
    }
}
