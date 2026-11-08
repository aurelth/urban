using FluentAssertions;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class YearMonthTests
{
    [Fact]
    public void Create_AValidMonth_KeepsYearAndMonth()
    {
        Result<YearMonth> result = YearMonth.Create(2026, 9);

        result.IsSuccess.Should().BeTrue();
        result.Value.Year.Should().Be(2026);
        result.Value.Month.Should().Be(9);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(2026, -1)]
    [InlineData(1999, 6)]
    [InlineData(2201, 6)]
    public void Create_AnInvalidMonthOrYear_Fails(int year, int month)
    {
        Result<YearMonth> result = YearMonth.Create(year, month);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MonthErrors.Invalid);
    }

    [Theory]
    [InlineData("2026-09", 2026, 9)]
    [InlineData(" 2026-12 ", 2026, 12)]
    public void Parse_TheYearDashMonthFormat_ReturnsTheMonth(string text, int year, int month)
    {
        Result<YearMonth> result = YearMonth.Parse(text);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(YearMonth.Create(year, month).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-9")]
    [InlineData("2026-13")]
    [InlineData("2026-09-01")]
    [InlineData("09/2026")]
    [InlineData("abc")]
    public void Parse_AnythingElse_Fails(string? text)
    {
        YearMonth.Parse(text).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public void ToString_UsesTheYearDashMonthFormat()
    {
        YearMonth.Create(2026, 9).Value.ToString().Should().Be("2026-09");
    }

    [Theory]
    [InlineData(2026, 11, 2, 2027, 1)]
    [InlineData(2026, 1, -1, 2025, 12)]
    [InlineData(2026, 5, 0, 2026, 5)]
    [InlineData(2026, 12, 12, 2027, 12)]
    [InlineData(2026, 3, -15, 2024, 12)]
    public void AddMonths_MovesAcrossYears(int year, int month, int months, int expectedYear, int expectedMonth)
    {
        YearMonth.Create(year, month).Value.AddMonths(months).Should().Be(YearMonth.Create(expectedYear, expectedMonth).Value);
    }

    [Fact]
    public void MonthsUntil_CountsTheDistanceInBothDirections()
    {
        YearMonth september = YearMonth.Create(2026, 9).Value;
        YearMonth march = YearMonth.Create(2027, 3).Value;

        september.MonthsUntil(march).Should().Be(6);
        march.MonthsUntil(september).Should().Be(-6);
        september.MonthsUntil(september).Should().Be(0);
    }

    [Fact]
    public void Comparison_OrdersByYearThenMonth()
    {
        YearMonth december2026 = YearMonth.Create(2026, 12).Value;
        YearMonth january2027 = YearMonth.Create(2027, 1).Value;
        YearMonth anotherDecember2026 = YearMonth.Create(2026, 12).Value;
        YearMonth anotherJanuary2027 = YearMonth.Create(2027, 1).Value;

        (december2026 < january2027).Should().BeTrue();
        (january2027 > december2026).Should().BeTrue();
        (december2026 <= anotherDecember2026).Should().BeTrue();
        (january2027 >= anotherJanuary2027).Should().BeTrue();
        december2026.CompareTo(january2027).Should().BeNegative();
    }

    [Fact]
    public void From_ADate_UsesItsMonth()
    {
        YearMonth.From(new DateOnly(2026, 9, 15)).Should().Be(YearMonth.Create(2026, 9).Value);
    }

    [Fact]
    public void FirstDay_IsTheFirstOfTheMonth()
    {
        YearMonth.Create(2026, 9).Value.FirstDay.Should().Be(new DateOnly(2026, 9, 1));
    }
}
