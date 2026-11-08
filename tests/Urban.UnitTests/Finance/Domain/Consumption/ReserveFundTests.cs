using FluentAssertions;
using Urban.Finance.Domain.Consumption;

namespace Urban.UnitTests.Finance.Domain.Consumption;

public class ReserveFundTests
{
    [Fact]
    public void AmountFor_IsThePercentageOfTheBase_RoundedToCents()
    {
        // Planilha de setembro/2026: 15% de R$ 5.894,06 = 884,109, cobrado como R$ 884,11.
        ReserveFund.AmountFor(5894.06m, 15m).Should().Be(884.11m);
    }

    [Theory]
    [InlineData("0", "100", "0")]
    [InlineData("1000", "0", "0")]
    [InlineData("1000", "100", "1000")]
    [InlineData("1000.01", "12.5", "125.00")]
    public void AmountFor_HandlesTheEdges(string baseTotal, string percent, string expected)
    {
        ReserveFund.AmountFor(decimal.Parse(baseTotal, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture))
            .Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("15")]
    [InlineData("100")]
    [InlineData("7.55")]
    public void IsValidPercent_AcceptsFromZeroToOneHundredWithTwoDecimals(string percent)
    {
        ReserveFund.IsValidPercent(decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture)).Should().BeTrue();
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("15.555")]
    public void IsValidPercent_RejectsTheRest(string percent)
    {
        ReserveFund.IsValidPercent(decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture)).Should().BeFalse();
    }
}
