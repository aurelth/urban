using FluentAssertions;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

public class VehicleTests
{
    private static readonly DateTimeOffset Now = VehicleBuilders.Now;

    [Fact]
    public void Create_NormalizesTheDataAndKeepsTheCondominiumAndTheUnit()
    {
        Guid condominiumId = Guid.CreateVersion7();
        Guid unitId = Guid.CreateVersion7();

        Result<Vehicle> result = Vehicle.Create(condominiumId, unitId, " abc-1d23 ", "  Fiat   Argo ", " Prata ", Now);

        result.IsSuccess.Should().BeTrue();
        Vehicle vehicle = result.Value;
        vehicle.Id.Should().NotBeEmpty();
        vehicle.CondominiumId.Should().Be(condominiumId);
        vehicle.UnitId.Should().Be(unitId);
        vehicle.Plate.Should().Be("ABC1D23");
        vehicle.Model.Should().Be("Fiat Argo");
        vehicle.Color.Should().Be("Prata");
        vehicle.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_WithoutACondominium_Fails() =>
        Vehicle.Create(Guid.Empty, Guid.CreateVersion7(), "ABC1234", "Gol", "Preto", Now).Error.Should().Be(VehicleErrors.CondominiumRequired);

    [Fact]
    public void Create_WithoutAUnit_Fails() =>
        Vehicle.Create(Guid.CreateVersion7(), Guid.Empty, "ABC1234", "Gol", "Preto", Now).Error.Should().Be(VehicleErrors.UnitRequired);

    [Fact]
    public void Create_ReportsTheFirstInvalidFieldInTheOrderOfTheForm_PlateThenModelThenColor()
    {
        Vehicle.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "x", "", "", Now).Error.Should().Be(VehicleErrors.PlateInvalid);
        Vehicle.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "ABC1234", "", "", Now).Error.Should().Be(VehicleErrors.ModelInvalid);
        Vehicle.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "ABC1234", "Gol", "", Now).Error.Should().Be(VehicleErrors.ColorInvalid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("A", false)]
    [InlineData("Up", true)]
    [InlineData("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX", false)]
    [InlineData("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX", true)]
    public void Normalize_ModelNeedsTwoToSixtyCharacters(string model, bool valid)
    {
        Result<VehicleData> result = Vehicle.Normalize("ABC1234", model, "Preto");

        result.IsSuccess.Should().Be(valid);
        if (!valid)
        {
            result.Error.Should().Be(VehicleErrors.ModelInvalid);
        }
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("P", false)]
    [InlineData("Cinza", true)]
    [InlineData("Azul metalico escuro bem escuro", false)]
    [InlineData("Azul metalico escuro bem escur", true)]
    public void Normalize_ColorNeedsTwoToThirtyCharacters(string color, bool valid)
    {
        Result<VehicleData> result = Vehicle.Normalize("ABC1234", "Gol", color);

        result.IsSuccess.Should().Be(valid);
        if (!valid)
        {
            result.Error.Should().Be(VehicleErrors.ColorInvalid);
        }
    }

    [Fact]
    public void Normalize_RejectsControlCharactersInTheModelAndTheColor()
    {
        Vehicle.Normalize("ABC1234", "Gol\u0001", "Preto").Error.Should().Be(VehicleErrors.ModelInvalid);
        Vehicle.Normalize("ABC1234", "Gol", "Pre\u0007to").Error.Should().Be(VehicleErrors.ColorInvalid);
    }

    [Fact]
    public void Update_ChangesEverythingAtOnce()
    {
        Vehicle vehicle = VehicleBuilders.Build();
        Guid newUnit = Guid.CreateVersion7();

        Result result = vehicle.Update(newUnit, "xyz-9876", " Honda  Civic ", "Branco");

        result.IsSuccess.Should().BeTrue();
        vehicle.UnitId.Should().Be(newUnit);
        vehicle.Plate.Should().Be("XYZ9876");
        vehicle.Model.Should().Be("Honda Civic");
        vehicle.Color.Should().Be("Branco");
    }

    [Fact]
    public void Update_WithAnInvalidField_ChangesNothing()
    {
        Vehicle vehicle = VehicleBuilders.Build(plate: "ABC1234", model: "Gol", color: "Preto");
        Guid unit = vehicle.UnitId;

        Result result = vehicle.Update(Guid.CreateVersion7(), "ABC1D23", "Gol", "");

        result.Error.Should().Be(VehicleErrors.ColorInvalid);
        vehicle.UnitId.Should().Be(unit);
        vehicle.Plate.Should().Be("ABC1234");
    }

    [Fact]
    public void Update_WithoutAUnit_Fails()
    {
        Vehicle vehicle = VehicleBuilders.Build();

        vehicle.Update(Guid.Empty, "ABC1234", "Gol", "Preto").Error.Should().Be(VehicleErrors.UnitRequired);
    }
}
