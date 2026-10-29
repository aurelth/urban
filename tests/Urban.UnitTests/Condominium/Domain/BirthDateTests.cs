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
}
