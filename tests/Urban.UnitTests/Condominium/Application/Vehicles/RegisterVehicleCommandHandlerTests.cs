using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Vehicles;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Vehicles;

public class RegisterVehicleCommandHandlerTests
{
    private readonly VehiclesTestKit _kit = new();
    private readonly CondominiumUnit _unit;

    public RegisterVehicleCommandHandlerTests()
    {
        _unit = _kit.ActiveUnit("101", "Bloco A");
    }

    private RegisterVehicleCommandHandler Handler() => new(_kit.Vehicles.Object, _kit.Units.Object, _kit.Tenant, _kit.Clock.Object);

    private RegisterVehicleCommand Valid() => new(_kit.ActorId, _unit.Id, "abc-1d23", " Fiat  Argo ", "Prata");

    [Fact]
    public async Task Handle_RegistersTheVehicleInTheRequestCondominium_WithTheDataNormalizedAndTheUnitOnTheView()
    {
        Result<VehicleView> result = await Handler().Handle(Valid(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Plate.Should().Be("ABC1D23");
        result.Value.Model.Should().Be("Fiat Argo");
        result.Value.Color.Should().Be("Prata");
        result.Value.UnitId.Should().Be(_unit.Id);
        result.Value.UnitBlock.Should().Be("Bloco A");
        result.Value.UnitNumber.Should().Be("101");
        result.Value.Version.Should().Be("7");
        Vehicle added = _kit.Added.Should().ContainSingle().Subject;
        added.CondominiumId.Should().Be(_kit.CondominiumId);
        result.Value.Id.Should().Be(added.Id);
    }

    [Fact]
    public async Task Handle_LogsOnlyTheFieldNames_NeverTheValues()
    {
        await Handler().Handle(Valid(), default);

        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Vehicle);
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(_kit.ActorId);
        entry.SubjectId.Should().Be(_kit.Added.Single().Id);
        entry.At.Should().Be(VehiclesTestKit.Now);
        entry.Fields.Should().BeEquivalentTo("unit", "plate", "model", "color");
    }

    [Fact]
    public async Task Handle_WithoutACondominium_FailsWithNotFound()
    {
        _kit.WithoutATenant();

        (await Handler().Handle(Valid(), default)).Error.Should().Be(CondominiumErrors.NotFound);
        _kit.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAnInvalidField_FailsBeforeTouchingTheDatabase()
    {
        Result<VehicleView> result = await Handler().Handle(Valid() with { Plate = "ABC12" }, default);

        result.Error.Should().Be(VehicleErrors.PlateInvalid);
        _kit.Units.Verify(u => u.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _kit.Vehicles.Verify(v => v.IsPlateTakenAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _kit.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutAUnit_FailsWithUnitRequired() =>
        (await Handler().Handle(Valid() with { UnitId = Guid.Empty }, default)).Error.Should().Be(VehicleErrors.UnitRequired);

    [Fact]
    public async Task Handle_WithAUnitThatIsNotInTheCondominium_FailsWithUnitNotFound()
    {
        Result<VehicleView> result = await Handler().Handle(Valid() with { UnitId = Guid.CreateVersion7() }, default);

        result.Error.Should().Be(UnitErrors.NotFound);
        _kit.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithADeactivatedUnit_FailsWithUnitDeactivated()
    {
        CondominiumUnit deactivated = _kit.DeactivatedUnit();

        Result<VehicleView> result = await Handler().Handle(Valid() with { UnitId = deactivated.Id }, default);

        result.Error.Should().Be(UnitErrors.Deactivated);
        _kit.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAPlateThatIsAlreadyRegistered_FailsWithAConflict_AndSavesNothing()
    {
        _kit.Vehicles.Setup(v => v.IsPlateTakenAsync("ABC1D23", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Result<VehicleView> result = await Handler().Handle(Valid(), default);

        result.Error.Should().Be(VehicleErrors.PlateAlreadyRegistered);
        _kit.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheDatabaseSaysTheSamePlateWasTakenInTheMeantime_ReturnsThatConflict()
    {
        _kit.Vehicles
            .Setup(v => v.AddAsync(It.IsAny<Vehicle>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(VehicleErrors.PlateAlreadyRegistered));

        (await Handler().Handle(Valid(), default)).Error.Should().Be(VehicleErrors.PlateAlreadyRegistered);
    }
}
