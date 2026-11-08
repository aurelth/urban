using FluentAssertions;
using Urban.Finance.Domain.Consumption;

namespace Urban.UnitTests.Finance.Domain.Consumption;

public class GasTariffTests
{
    [Fact]
    public void PricePerCubicMeter_IsTheKilosPerCubicMeterTimesThePricePerKilo_RoundedToCents()
    {
        // 2,4 kg/m³ × R$ 15,71/kg = 37,704, que a síndica cobra como R$ 37,70.
        GasTariff.Create(2.4m, 15.71m).Value.PricePerCubicMeter.Should().Be(37.70m);
    }

    [Fact]
    public void ChargeFor_UsesThePriceInCents_AsTheSpreadsheetDoes()
    {
        GasTariff tariff = GasTariff.Create(2.4m, 15.71m).Value;

        tariff.ChargeFor(2.649m).Should().Be(99.87m);
        tariff.ChargeFor(0.782m).Should().Be(29.48m);
        tariff.ChargeFor(0m).Should().Be(0m);
    }

    [Fact]
    public void Create_WithOneKiloPerCubicMeter_TheSecondValueIsThePricePerCubicMeter()
    {
        GasTariff.Create(1m, 37.70m).Value.PricePerCubicMeter.Should().Be(37.70m);
    }

    [Theory]
    [InlineData(0, 15.71)]
    [InlineData(-1, 15.71)]
    [InlineData(2.4, 0)]
    [InlineData(2.4, -3)]
    [InlineData(2.4, 15.713)]
    [InlineData(2.4567, 15.71)]
    [InlineData(1000, 15.71)]
    [InlineData(2.4, 1000)]
    public void Create_RejectsValuesOutOfRange(double kilos, double price)
    {
        GasTariff.Create((decimal)kilos, (decimal)price).Error.Should().Be(ConsumptionErrors.GasInvalid);
    }
}
