using FluentAssertions;
using Urban.Finance.Domain.Consumption;

namespace Urban.UnitTests.Finance.Domain.Consumption;

public class TariffScaleTests
{
    // Tabela SAMAE 2026, residencial: até 10 m³ valor fixo; de 11 a 30 m³ e acima de 30 m³ por m³.
    private static readonly TariffBand[] Water =
    [
        new(10m, 47.43m, 0m),
        new(30m, 0m, 8.63m),
        new(null, 0m, 11.00m),
    ];

    private static readonly TariffBand[] Sewage =
    [
        new(10m, 52.63m, 0m),
        new(30m, 0m, 9.635m),
        new(null, 0m, 12.279m),
    ];

    private static TariffScale Scale(params TariffBand[] bands) => TariffScale.Create(bands).Value;

    [Theory]
    [InlineData("0", "47.43")]
    [InlineData("4.973", "47.43")]
    [InlineData("10", "47.43")]
    [InlineData("11", "56.06")]
    [InlineData("17.649", "113.44")]
    [InlineData("30", "220.03")]
    [InlineData("35", "275.03")]
    public void ChargeFor_Water_FollowsTheBandsOfTheSamaeTable(string cubicMeters, string expected)
    {
        Scale(Water).ChargeFor(decimal.Parse(cubicMeters, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("0", "52.63")]
    [InlineData("10", "52.63")]
    [InlineData("17.649", "126.33")]
    [InlineData("30", "245.33")]
    [InlineData("35", "306.73")]
    public void ChargeFor_Sewage_UsesThePriceWithThreeDecimals(string cubicMeters, string expected)
    {
        Scale(Sewage).ChargeFor(decimal.Parse(cubicMeters, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ChargeFor_ChargesTheFixedAmountOfABandOnlyWhenTheConsumptionReachesIt()
    {
        TariffScale scale = Scale(new TariffBand(10m, 20m, 0m), new TariffBand(null, 5m, 2m));

        scale.ChargeFor(10m).Should().Be(20m);
        scale.ChargeFor(12m).Should().Be(29m);
    }

    [Fact]
    public void ChargeFor_WithASingleOpenBand_ChargesPerCubicMeter()
    {
        Scale(new TariffBand(null, 0m, 37.70m)).ChargeFor(2.649m).Should().Be(99.87m);
    }

    [Fact]
    public void ChargeFor_RoundsHalfAwayFromZeroToCents()
    {
        Scale(new TariffBand(null, 0m, 0.005m)).ChargeFor(1m).Should().Be(0.01m);
    }

    [Fact]
    public void Create_WithoutBands_Fails()
    {
        TariffScale.Create([]).Error.Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Fact]
    public void Create_TheLastBandMustHaveNoLimit()
    {
        TariffScale.Create([new TariffBand(10m, 47.43m, 0m), new TariffBand(30m, 0m, 8.63m)]).Error
            .Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Fact]
    public void Create_OnlyTheLastBandMayHaveNoLimit()
    {
        TariffScale.Create([new TariffBand(null, 47.43m, 0m), new TariffBand(null, 0m, 8.63m)]).Error
            .Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Fact]
    public void Create_TheLimitsMustGrowFromBandToBand()
    {
        TariffScale.Create([new TariffBand(30m, 0m, 8.63m), new TariffBand(10m, 0m, 9m), new TariffBand(null, 0m, 11m)]).Error
            .Should().Be(ConsumptionErrors.BandsInvalid);
        TariffScale.Create([new TariffBand(10m, 0m, 8.63m), new TariffBand(10m, 0m, 9m), new TariffBand(null, 0m, 11m)]).Error
            .Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_ALimitMustBePositive(int limit)
    {
        TariffScale.Create([new TariffBand(limit, 0m, 8.63m), new TariffBand(null, 0m, 11m)]).Error
            .Should().Be(ConsumptionErrors.BandsInvalid);
    }

    [Fact]
    public void Create_AFixedAmountHasAtMostTwoDecimalsAndIsNotNegative()
    {
        TariffScale.Create([new TariffBand(null, 47.431m, 0m)]).IsFailure.Should().BeTrue();
        TariffScale.Create([new TariffBand(null, -1m, 0m)]).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_APriceHasAtMostFourDecimalsAndIsNotNegative()
    {
        TariffScale.Create([new TariffBand(null, 0m, 9.63555m)]).IsFailure.Should().BeTrue();
        TariffScale.Create([new TariffBand(null, 0m, -0.1m)]).IsFailure.Should().BeTrue();
        TariffScale.Create([new TariffBand(null, 0m, 9.6355m)]).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_AcceptsAtMostSixBands()
    {
        TariffBand[] seven = [.. Enumerable.Range(1, 6).Select(i => new TariffBand(i * 10m, 0m, i)), new TariffBand(null, 0m, 7m)];
        TariffBand[] six = [.. Enumerable.Range(1, 5).Select(i => new TariffBand(i * 10m, 0m, i)), new TariffBand(null, 0m, 6m)];

        TariffScale.Create(seven).IsFailure.Should().BeTrue();
        TariffScale.Create(six).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_KeepsTheBandsAsGiven()
    {
        Scale(Water).Bands.Should().Equal(Water);
    }
}
