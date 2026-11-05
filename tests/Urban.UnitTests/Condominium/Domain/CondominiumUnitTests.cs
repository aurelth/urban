using FluentAssertions;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>O cadastro e a edição de uma unidade (apartamento ou casa) do condomínio.</summary>
public class CondominiumUnitTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private static Result<CondominiumUnit> Create(
        string? block = null,
        string? number = "101",
        UnitType type = UnitType.Apartment,
        decimal? idealFraction = null,
        decimal? area = null,
        Guid? condominiumId = null) =>
        CondominiumUnit.Create(condominiumId ?? CondominiumId, block, number, type, idealFraction, area, Now);

    // ---- Cadastro ----

    [Fact]
    public void Create_AnApartmentWithBlockNumberFractionAndArea_StoresEverythingAndStartsActive()
    {
        Result<CondominiumUnit> result = Create(block: "Bloco A", number: "101", UnitType.Apartment, idealFraction: 0.0125m, area: 85.5m);

        result.IsSuccess.Should().BeTrue();
        CondominiumUnit unit = result.Value;
        unit.Id.Should().NotBe(Guid.Empty);
        unit.CondominiumId.Should().Be(CondominiumId);
        unit.Block.Should().Be("Bloco A");
        unit.Number.Should().Be("101");
        unit.Type.Should().Be(UnitType.Apartment);
        unit.IdealFraction.Should().Be(0.0125m);
        unit.Area.Should().Be(85.5m);
        unit.CreatedAt.Should().Be(Now);
        unit.IsActive.Should().BeTrue();
        unit.DeactivatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_AHouseWithoutBlockFractionAndArea_Succeeds()
    {
        Result<CondominiumUnit> result = Create(number: "Casa 7", type: UnitType.House);

        result.IsSuccess.Should().BeTrue();
        result.Value.Block.Should().BeNull();
        result.Value.Type.Should().Be(UnitType.House);
        result.Value.IdealFraction.Should().BeNull();
        result.Value.Area.Should().BeNull();
    }

    [Fact]
    public void Create_TrimsTheEdgesAndCollapsesRepeatedSpacesInTheNumberAndInTheBlock()
    {
        CondominiumUnit unit = Create(block: "  Bloco   A ", number: "  Casa    7  ").Value;

        unit.Block.Should().Be("Bloco A");
        unit.Number.Should().Be("Casa 7");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithAnEmptyNumber_Fails(string? number)
    {
        Create(number: number).Error.Should().Be(UnitErrors.NumberInvalid);
    }

    [Fact]
    public void Create_WithANumberLongerThanTwentyCharacters_Fails()
    {
        Create(number: new string('1', 21)).Error.Should().Be(UnitErrors.NumberInvalid);
        Create(number: new string('1', 20)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithAControlCharacterInTheNumberOrInTheBlock_Fails()
    {
        Create(number: "10\u00071").Error.Should().Be(UnitErrors.NumberInvalid);
        Create(block: "Bloco\u0007 A").Error.Should().Be(UnitErrors.BlockInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_TreatsAnEmptyBlockAsNoBlock(string? block)
    {
        Create(block: block).Value.Block.Should().BeNull();
    }

    [Fact]
    public void Create_WithABlockLongerThanThirtyCharacters_Fails()
    {
        Create(block: new string('A', 31)).Error.Should().Be(UnitErrors.BlockInvalid);
        Create(block: new string('A', 30)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Create_WithATypeOtherThanApartmentOrHouse_Fails(int type)
    {
        Create(type: (UnitType)type).Error.Should().Be(UnitErrors.TypeInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Create_WithAFractionZeroOrNegative_Fails(double fraction)
    {
        Create(idealFraction: (decimal)fraction).Error.Should().Be(UnitErrors.IdealFractionInvalid);
    }

    [Fact]
    public void Create_WithAFractionAboveOneHundredPercent_Fails()
    {
        Create(idealFraction: 1.00000001m).Error.Should().Be(UnitErrors.IdealFractionInvalid);
        Create(idealFraction: 1m).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMoreThanEightDecimalPlacesInTheFraction_Fails()
    {
        Create(idealFraction: 0.123456789m).Error.Should().Be(UnitErrors.IdealFractionInvalid);
        Create(idealFraction: 0.12345678m).IsSuccess.Should().BeTrue();
        Create(idealFraction: 0.12000000m).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100000)]
    public void Create_WithAnAreaZeroNegativeOrAboveTheLimit_Fails(double area)
    {
        Create(area: (decimal)area).Error.Should().Be(UnitErrors.AreaInvalid);
    }

    [Fact]
    public void Create_WithTheLargestAcceptedArea_Succeeds()
    {
        Create(area: 99999.99m).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMoreThanTwoDecimalPlacesInTheArea_Fails()
    {
        Create(area: 85.555m).Error.Should().Be(UnitErrors.AreaInvalid);
        Create(area: 85.55m).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutACondominium_Fails()
    {
        Create(condominiumId: Guid.Empty).Error.Should().Be(UnitErrors.CondominiumRequired);
    }

    [Fact]
    public void Normalize_ReturnsTheSameValuesThatCreateWouldStore_WithoutCreatingAnything()
    {
        Result<UnitData> data = CondominiumUnit.Normalize("  Bloco  B ", " 12  ", UnitType.House, 0.5m, 70m);

        data.IsSuccess.Should().BeTrue();
        data.Value.Should().Be(new UnitData("Bloco B", "12", UnitType.House, 0.5m, 70m));
        CondominiumUnit.Normalize(null, "", UnitType.Apartment, null, null).Error.Should().Be(UnitErrors.NumberInvalid);
    }

    // ---- Edição e situação ----

    [Fact]
    public void Update_ChangesBlockNumberTypeFractionAndArea()
    {
        CondominiumUnit unit = Create(block: "A", number: "101", idealFraction: 0.01m, area: 50m).Value;

        Result result = unit.Update(" Bloco B ", " 202 ", UnitType.House, 0.02m, 60.5m);

        result.IsSuccess.Should().BeTrue();
        unit.Block.Should().Be("Bloco B");
        unit.Number.Should().Be("202");
        unit.Type.Should().Be(UnitType.House);
        unit.IdealFraction.Should().Be(0.02m);
        unit.Area.Should().Be(60.5m);
    }

    [Fact]
    public void Update_CanClearTheFractionTheAreaAndTheBlock()
    {
        CondominiumUnit unit = Create(block: "A", idealFraction: 0.01m, area: 50m).Value;

        unit.Update(null, "101", UnitType.Apartment, null, null).IsSuccess.Should().BeTrue();

        unit.Block.Should().BeNull();
        unit.IdealFraction.Should().BeNull();
        unit.Area.Should().BeNull();
    }

    [Theory]
    [InlineData("", UnitType.Apartment, 0.02, 60)]
    [InlineData("202", UnitType.Apartment, 2, 60)]
    [InlineData("202", UnitType.Apartment, 0.02, -5)]
    [InlineData("202", (UnitType)9, 0.02, 60)]
    public void Update_WithInvalidData_FailsAndChangesNothing(string number, UnitType type, double fraction, double area)
    {
        CondominiumUnit unit = Create(block: "A", number: "101", idealFraction: 0.01m, area: 50m).Value;

        Result result = unit.Update("B", number, type, (decimal)fraction, (decimal)area);

        result.IsFailure.Should().BeTrue();
        unit.Block.Should().Be("A");
        unit.Number.Should().Be("101");
        unit.Type.Should().Be(UnitType.Apartment);
        unit.IdealFraction.Should().Be(0.01m);
        unit.Area.Should().Be(50m);
    }

    [Fact]
    public void Update_OfADeactivatedUnit_Fails()
    {
        CondominiumUnit unit = Create().Value;
        unit.Deactivate(Now);

        unit.Update(null, "999", UnitType.Apartment, null, null).Error.Should().Be(UnitErrors.Deactivated);
        unit.Number.Should().Be("101");
    }

    [Fact]
    public void Deactivate_AnActiveUnit_DeactivatesItAndKeepsTheDate()
    {
        CondominiumUnit unit = Create().Value;
        DateTimeOffset later = Now.AddDays(3);

        Result result = unit.Deactivate(later);

        result.IsSuccess.Should().BeTrue();
        unit.IsActive.Should().BeFalse();
        unit.DeactivatedAt.Should().Be(later);
    }

    [Fact]
    public void Deactivate_AnAlreadyDeactivatedUnit_FailsAndKeepsTheFirstDate()
    {
        CondominiumUnit unit = Create().Value;
        unit.Deactivate(Now.AddDays(1));

        Result result = unit.Deactivate(Now.AddDays(2));

        result.Error.Should().Be(UnitErrors.AlreadyDeactivated);
        unit.DeactivatedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void Reactivate_ADeactivatedUnit_ReactivatesItAndClearsTheDate()
    {
        CondominiumUnit unit = Create().Value;
        unit.Deactivate(Now.AddDays(1));

        Result result = unit.Reactivate();

        result.IsSuccess.Should().BeTrue();
        unit.IsActive.Should().BeTrue();
        unit.DeactivatedAt.Should().BeNull();
    }

    [Fact]
    public void Reactivate_AnActiveUnit_Fails()
    {
        CondominiumUnit unit = Create().Value;

        unit.Reactivate().Error.Should().Be(UnitErrors.AlreadyActive);
        unit.IsActive.Should().BeTrue();
    }
}
