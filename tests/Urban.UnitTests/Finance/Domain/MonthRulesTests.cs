using FluentAssertions;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class MonthRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    [Theory]
    [InlineData(2026, 10)]
    [InlineData(2026, 11)]
    [InlineData(2027, 11)]
    [InlineData(2030, 11)]
    [InlineData(2031, 11)]
    [InlineData(2025, 1)]
    public void CanOpen_UpToSixtyMonthsAhead_Succeeds(int year, int month)
    {
        Result result = MonthRules.CanOpen(YearMonth.Create(year, month).Value, Today);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(2031, 12)]
    [InlineData(2032, 1)]
    [InlineData(2040, 6)]
    public void CanOpen_FurtherThanSixtyMonthsAhead_Fails(int year, int month)
    {
        MonthRules.CanOpen(YearMonth.Create(year, month).Value, Today).Error.Should().Be(MonthErrors.TooFarAhead);
    }

    [Fact]
    public void CanOpen_TheLimitMovesWithTheCurrentMonth()
    {
        DateOnly december = new(2026, 12, 20);

        MonthRules.CanOpen(YearMonth.Create(2031, 12).Value, december).IsSuccess.Should().BeTrue();
        MonthRules.CanOpen(YearMonth.Create(2032, 1).Value, december).Error.Should().Be(MonthErrors.TooFarAhead);
    }

    [Fact]
    public void MaxMonthsAhead_IsFiveYears()
    {
        MonthRules.MaxMonthsAhead.Should().Be(60);
    }
}
