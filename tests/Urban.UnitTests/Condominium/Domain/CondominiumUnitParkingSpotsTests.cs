using FluentAssertions;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>As vagas de garagem de uma unidade: até 5, opcionais, cada uma com identificação curta e sem repetir.</summary>
public class CondominiumUnitParkingSpotsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static Result<CondominiumUnit> Create(params string?[]? spots) =>
        CondominiumUnit.Create(Guid.CreateVersion7(), null, "101", UnitType.Apartment, null, null, Now, spots);

    [Fact]
    public void Create_WithoutSpots_HasNone()
    {
        CondominiumUnit.Create(Guid.CreateVersion7(), null, "101", UnitType.Apartment, null, null, Now).Value.ParkingSpots.Should().BeEmpty();
        Create().Value.ParkingSpots.Should().BeEmpty();
    }

    [Fact]
    public void Create_KeepsTheSpotsInTheOrderGiven_TrimmedAndWithRepeatedSpacesCollapsed()
    {
        CondominiumUnit unit = Create("  G2   05 ", "V1", "12").Value;

        unit.ParkingSpots.Should().Equal("G2 05", "V1", "12");
    }

    [Fact]
    public void Create_AcceptsUpToFiveSpots_AndRefusesTheSixth()
    {
        Create("1", "2", "3", "4", "5").IsSuccess.Should().BeTrue();
        Create("1", "2", "3", "4", "5", "6").Error.Should().Be(UnitErrors.ParkingSpotsTooMany);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123456789012345678901")]
    [InlineData("V\u00071")]
    public void Create_RefusesABlankTooLongOrControlCharacterSpot(string? spot)
    {
        Create("V1", spot).Error.Should().Be(UnitErrors.ParkingSpotInvalid);
    }

    [Fact]
    public void Create_AcceptsASpotWithTheMaximumLength()
    {
        Create(new string('9', CondominiumUnit.ParkingSpotMaxLength)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_RefusesTheSameSpotTwice_IgnoringCaseAndSpacing()
    {
        Create("V1", "v1").Error.Should().Be(UnitErrors.ParkingSpotsDuplicate);
        Create("G 2", " g   2 ").Error.Should().Be(UnitErrors.ParkingSpotsDuplicate);
    }

    [Fact]
    public void Create_ReportsTheTooManyErrorBeforeTheInvalidOne()
    {
        Create("1", "2", "3", "4", "5", " ").Error.Should().Be(UnitErrors.ParkingSpotsTooMany);
    }

    [Fact]
    public void Normalize_ReturnsTheSpotsInTheData()
    {
        Result<UnitData> data = CondominiumUnit.Normalize(null, "101", UnitType.Apartment, null, null, [" A1 ", "A2"]);

        data.Value.ParkingSpots.Should().Equal("A1", "A2");
        CondominiumUnit.Normalize(null, "101", UnitType.Apartment, null, null).Value.ParkingSpots.Should().BeEmpty();
    }

    [Fact]
    public void Update_ReplacesTheSpots()
    {
        CondominiumUnit unit = Create("A1", "A2").Value;

        unit.Update(null, "101", UnitType.Apartment, null, null, ["B7"]).IsSuccess.Should().BeTrue();

        unit.ParkingSpots.Should().Equal("B7");
    }

    [Fact]
    public void Update_WithAnEmptyList_RemovesThem_AndWithNullKeepsThem()
    {
        CondominiumUnit unit = Create("A1", "A2").Value;

        unit.Update(null, "101", UnitType.Apartment, null, null).IsSuccess.Should().BeTrue();
        unit.ParkingSpots.Should().Equal("A1", "A2");

        unit.Update(null, "101", UnitType.Apartment, null, null, []).IsSuccess.Should().BeTrue();
        unit.ParkingSpots.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithInvalidSpots_ChangesNothing()
    {
        CondominiumUnit unit = Create("A1").Value;

        unit.Update("Bloco B", "202", UnitType.House, null, null, ["A1", "a1"]).Error.Should().Be(UnitErrors.ParkingSpotsDuplicate);

        unit.ParkingSpots.Should().Equal("A1");
        unit.Number.Should().Be("101");
        unit.Block.Should().BeNull();
    }

    [Fact]
    public void TheTakenError_CarriesTheSpotThatIsTaken()
    {
        Error error = UnitErrors.ParkingSpotTaken("G2 05");

        error.Code.Should().Be("unit.parking_spot.already_exists");
        error.Data.Should().ContainKey("spot").WhoseValue.Should().Be("G2 05");
    }
}
