using FluentAssertions;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class BirthDateTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);

    [Fact]
    public void Create_OnTheEighteenthBirthday_Succeeds()
    {
        Result<BirthDate> result = BirthDate.Create(new DateOnly(2008, 10, 29), Today);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(new DateOnly(2008, 10, 29));
    }

    [Fact]
    public void Create_OneDayBeforeTheEighteenthBirthday_FailsAsUnderAge()
    {
        Result<BirthDate> result = BirthDate.Create(new DateOnly(2008, 10, 30), Today);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BirthDate.UnderAge);
        BirthDate.UnderAge.Code.Should().Be("condominium.birth_date.under_age");
    }

    [Fact]
    public void Create_UnderAge_WithACustomError_ReturnsThatError()
    {
        Error custom = new("condominium.resident.under_age", "Morador menor de idade");

        Result<BirthDate> result = BirthDate.Create(new DateOnly(2008, 10, 30), Today, custom);

        result.Error.Should().Be(custom);
    }

    [Fact]
    public void Create_WithACustomUnderAgeError_StillReportsAnInvalidDateAsInvalid()
    {
        Error custom = new("condominium.resident.under_age", "Morador menor de idade");

        BirthDate.Create(Today.AddDays(1), Today, custom).Error.Should().Be(BirthDate.Invalid);
    }

    [Fact]
    public void Create_BornToday_FailsAsUnderAgeAndNotAsInvalid()
    {
        BirthDate.Create(Today, Today).Error.Should().Be(BirthDate.UnderAge);
    }

    [Fact]
    public void Create_BornOnFebruary29_TurnsEighteenOnMarch1InCommonYears()
    {
        DateOnly born = new(2008, 2, 29);

        BirthDate.Create(born, new DateOnly(2026, 2, 28)).IsFailure.Should().BeTrue();
        BirthDate.Create(born, new DateOnly(2026, 3, 1)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(2026, 10, 30)]
    [InlineData(2030, 1, 1)]
    [InlineData(1900, 1, 1)]
    [InlineData(1906, 10, 28)]
    public void Create_InTheFutureOrOlderThan120Years_FailsAsInvalid(int year, int month, int day)
    {
        Result<BirthDate> result = BirthDate.Create(new DateOnly(year, month, day), Today);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BirthDate.Invalid);
        BirthDate.Invalid.Code.Should().Be("condominium.birth_date.invalid");
    }

    [Fact]
    public void Create_ExactlyOn120Years_Succeeds()
    {
        BirthDate.Create(new DateOnly(1906, 10, 29), Today).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutADate_FailsAsInvalid()
    {
        BirthDate.Create(null, Today).Error.Should().Be(BirthDate.Invalid);
    }

    [Fact]
    public void Restore_KeepsTheStoredDateEvenWhenTheAgeRuleWouldNowRejectIt()
    {
        DateOnly stored = new(1900, 1, 1);

        BirthDate.Restore(stored).Value.Should().Be(stored);
    }

    [Theory]
    [InlineData(2026, 10, 29)]
    [InlineData(2015, 4, 10)]
    [InlineData(2008, 10, 30)]
    [InlineData(2008, 10, 29)]
    [InlineData(1990, 5, 20)]
    public void CreateAllowingMinor_AcceptsAnyAgeUpTo120Years(int year, int month, int day)
    {
        Result<BirthDate> result = BirthDate.CreateAllowingMinor(new DateOnly(year, month, day), Today);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(new DateOnly(year, month, day));
    }

    [Theory]
    [InlineData(2026, 10, 30)]
    [InlineData(2030, 1, 1)]
    [InlineData(1906, 10, 28)]
    public void CreateAllowingMinor_InTheFutureOrOlderThan120Years_FailsAsInvalid(int year, int month, int day)
    {
        BirthDate.CreateAllowingMinor(new DateOnly(year, month, day), Today).Error.Should().Be(BirthDate.Invalid);
    }

    [Fact]
    public void CreateAllowingMinor_WithoutADate_FailsAsInvalid()
    {
        BirthDate.CreateAllowingMinor(null, Today).Error.Should().Be(BirthDate.Invalid);
    }

    [Fact]
    public void IsMinorOn_IsTrueUntilTheDayBeforeTheEighteenthBirthday_AndFalseOnIt()
    {
        BirthDate born = BirthDate.CreateAllowingMinor(new DateOnly(2008, 10, 30), Today).Value;

        born.IsMinorOn(Today).Should().BeTrue();
        born.IsMinorOn(new DateOnly(2026, 10, 29)).Should().BeTrue();
        born.IsMinorOn(new DateOnly(2026, 10, 30)).Should().BeFalse();
        born.IsMinorOn(new DateOnly(2026, 10, 31)).Should().BeFalse();
    }

    [Fact]
    public void IsMinorOn_ForSomeoneBornOnFebruary29_TurnsAdultOnMarch1InCommonYears()
    {
        BirthDate born = BirthDate.CreateAllowingMinor(new DateOnly(2008, 2, 29), Today).Value;

        born.IsMinorOn(new DateOnly(2026, 2, 28)).Should().BeTrue();
        born.IsMinorOn(new DateOnly(2026, 3, 1)).Should().BeFalse();
    }

    [Fact]
    public void IsMinorOn_ForARestoredDate_UsesTheSameRule()
    {
        BirthDate.Restore(new DateOnly(2015, 4, 10)).IsMinorOn(Today).Should().BeTrue();
        BirthDate.Restore(new DateOnly(1990, 5, 20)).IsMinorOn(Today).Should().BeFalse();
    }
}
