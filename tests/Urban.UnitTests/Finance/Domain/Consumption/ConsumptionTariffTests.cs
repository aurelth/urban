using FluentAssertions;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;

namespace Urban.UnitTests.Finance.Domain.Consumption;

public class ConsumptionTariffTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly YearMonth January = YearMonth.Create(2026, 1).Value;

    private static TariffScale Water => TariffScale.Create([new TariffBand(10m, 47.43m, 0m), new TariffBand(30m, 0m, 8.63m), new TariffBand(null, 0m, 11m)]).Value;

    private static TariffScale Sewage => TariffScale.Create([new TariffBand(10m, 52.63m, 0m), new TariffBand(30m, 0m, 9.635m), new TariffBand(null, 0m, 12.279m)]).Value;

    private static GasTariff Gas => GasTariff.Create(2.4m, 15.71m).Value;

    private static ConsumptionTariff Create(YearMonth? from = null, TariffScale? water = null, TariffScale? sewage = null, decimal sewagePercent = 100m, GasTariff? gas = null) =>
        ConsumptionTariff.Create(CondominiumId, from ?? January, water ?? Water, sewage, sewagePercent, gas, Now).Value;

    [Fact]
    public void Create_KeepsEverythingAndAllowsSewageAndGasToBeOff()
    {
        ConsumptionTariff tariff = Create(sewage: Sewage, sewagePercent: 50m, gas: Gas);

        tariff.CondominiumId.Should().Be(CondominiumId);
        tariff.ValidFrom.Should().Be(January);
        tariff.Water.Bands.Should().HaveCount(3);
        tariff.Sewage.Should().NotBeNull();
        tariff.SewagePercent.Should().Be(50m);
        tariff.Gas!.PricePerCubicMeter.Should().Be(37.70m);
        tariff.CreatedAt.Should().Be(Now);

        ConsumptionTariff waterOnly = Create();
        waterOnly.Sewage.Should().BeNull();
        waterOnly.Gas.Should().BeNull();
    }

    [Fact]
    public void WaterCharge_FollowsTheWaterBands()
    {
        Create().WaterCharge(17.649m).Should().Be(113.44m);
    }

    [Fact]
    public void SewageCharge_IsTheSewageBandsTimesThePercentage()
    {
        ConsumptionTariff full = Create(sewage: Sewage, sewagePercent: 100m);
        ConsumptionTariff partial = Create(sewage: Sewage, sewagePercent: 50m);

        full.SewageCharge(17.649m).Should().Be(126.33m);
        partial.SewageCharge(17.649m).Should().Be(63.16m);
    }

    [Fact]
    public void SewageCharge_WithoutSewageIsZero()
    {
        Create().SewageCharge(17.649m).Should().Be(0m);
    }

    [Fact]
    public void GasCharge_WithoutGasIsZero()
    {
        Create().GasCharge(3m).Should().Be(0m);
        Create(gas: Gas).GasCharge(3.34m).Should().Be(125.92m);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("33.333")]
    public void Create_TheSewagePercentageIsFromZeroToOneHundredWithTwoDecimals(string percent)
    {
        TryCreate(decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture)).Error.Should().Be(ConsumptionErrors.SewagePercentInvalid);
    }

    [Fact]
    public void Create_RequiresTheCondominium()
    {
        ConsumptionTariff.Create(Guid.Empty, January, Water, null, 100m, null, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CurrentFor_PicksTheLatestTariffThatAlreadyStartedInTheMonth()
    {
        ConsumptionTariff first = Create(from: January);
        ConsumptionTariff second = Create(from: YearMonth.Create(2026, 7).Value);
        ConsumptionTariff third = Create(from: YearMonth.Create(2027, 1).Value);
        ConsumptionTariff[] all = [third, first, second];

        ConsumptionTariff.CurrentFor(YearMonth.Create(2026, 6).Value, all).Should().BeSameAs(first);
        ConsumptionTariff.CurrentFor(YearMonth.Create(2026, 7).Value, all).Should().BeSameAs(second);
        ConsumptionTariff.CurrentFor(YearMonth.Create(2026, 12).Value, all).Should().BeSameAs(second);
        ConsumptionTariff.CurrentFor(YearMonth.Create(2027, 3).Value, all).Should().BeSameAs(third);
    }

    [Fact]
    public void CurrentFor_ReturnsNullBeforeTheFirstTariffStarts()
    {
        ConsumptionTariff.CurrentFor(YearMonth.Create(2025, 12).Value, [Create(from: January)]).Should().BeNull();
        ConsumptionTariff.CurrentFor(January, []).Should().BeNull();
    }

    private static Urban.SharedKernel.Result<ConsumptionTariff> TryCreate(decimal percent) =>
        ConsumptionTariff.Create(CondominiumId, January, Water, Sewage, percent, null, Now);
}
