using FluentAssertions;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class LicensePlateTests
{
    [Theory]
    [InlineData("ABC1234", "ABC1234")]
    [InlineData("abc-1234", "ABC1234")]
    [InlineData(" ABC 1234 ", "ABC1234")]
    [InlineData("ABC1D23", "ABC1D23")]
    [InlineData("abc1d23", "ABC1D23")]
    [InlineData("ABC-1D23", "ABC1D23")]
    public void Normalize_AcceptsTheOldAndTheMercosulFormats_AndWritesThemInCapitalsWithoutSeparators(string typed, string expected)
    {
        Result<string> result = LicensePlate.Normalize(typed);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC123")]
    [InlineData("ABC12345")]
    [InlineData("AB1C234")]
    [InlineData("ABCD123")]
    [InlineData("1234ABC")]
    [InlineData("ABC1DE3")]
    [InlineData("ABC1234!")]
    [InlineData("ÁBC1234")]
    [InlineData("ABC١٢٣٤")]
    public void Normalize_RejectsAnythingThatIsNeitherFormat(string? typed)
    {
        Result<string> result = LicensePlate.Normalize(typed);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(VehicleErrors.PlateInvalid);
    }

    [Theory]
    [InlineData("ABC1234", false)]
    [InlineData("ABC1D23", true)]
    public void IsMercosul_TellsTheFormatApart(string plate, bool expected) =>
        LicensePlate.IsMercosul(plate).Should().Be(expected);
}
