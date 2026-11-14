using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Vehicles;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Vehicles;

public class UpdateVehicleCommandHandlerTests
{
    private readonly VehiclesTestKit _kit = new();
    private readonly CondominiumUnit _unit;
    private readonly VehicleRecord _record;

    public UpdateVehicleCommandHandlerTests()
    {
        _unit = _kit.ActiveUnit("101", "Bloco A");
        _record = _kit.Existing(_unit, "ABC1D23", "Fiat Argo", "Prata", version: "5");
    }

    private UpdateVehicleCommandHandler Handler() => new(_kit.Vehicles.Object, _kit.Units.Object, _kit.Tenant, _kit.Clock.Object);

    /// <summary>O mesmo veículo, sem nenhuma mudança: cada teste muda só o campo que quer provar.</summary>
    private UpdateVehicleCommand Same(string? version = "5") =>
        new(_kit.ActorId, _record.Vehicle.Id, version, _unit.Id, "ABC1D23", "Fiat Argo", "Prata");

    [Fact]
    public async Task Handle_WithoutACondominium_FailsWithNotFound()
    {
        _kit.WithoutATenant();

        (await Handler().Handle(Same(), default)).Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WithoutTheVersion_FailsWithVersionRequired()
    {
        (await Handler().Handle(Same(version: null), default)).Error.Should().Be(VehicleErrors.VersionRequired);
        (await Handler().Handle(Same(version: " "), default)).Error.Should().Be(VehicleErrors.VersionRequired);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAVehicleThatDoesNotExist_FailsWithNotFound() =>
        (await Handler().Handle(Same() with { VehicleId = Guid.CreateVersion7() }, default)).Error.Should().Be(VehicleErrors.NotFound);

    [Fact]
    public async Task Handle_WithAnOldVersion_FailsWithAConcurrencyConflict_AndChangesNothing()
    {
        Result<VehicleView> result = await Handler().Handle(Same(version: "4") with { Model = "Outro" }, default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _record.Vehicle.Model.Should().Be("Fiat Argo");
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAnInvalidField_FailsAndChangesNothing()
    {
        Result<VehicleView> result = await Handler().Handle(Same() with { Plate = "123" }, default);

        result.Error.Should().Be(VehicleErrors.PlateInvalid);
        _record.Vehicle.Plate.Should().Be("ABC1D23");
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNothingChanged_ReturnsTheCurrentDataWithoutSaving_AndTheVersionStays()
    {
        Result<VehicleView> result = await Handler().Handle(Same() with { Plate = "abc-1d23", Model = " Fiat  Argo " }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("5");
        result.Value.Plate.Should().Be("ABC1D23");
        _kit.Saved.Should().BeEmpty();
        _kit.Logged.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ChangesTheFields_ReturnsTheNewVersion_AndLogsOnlyWhatChanged()
    {
        Result<VehicleView> result = await Handler().Handle(Same() with { Plate = "XYZ9876", Color = "Branco" }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Plate.Should().Be("XYZ9876");
        result.Value.Color.Should().Be("Branco");
        result.Value.Model.Should().Be("Fiat Argo");
        result.Value.Version.Should().Be("8");
        _kit.Saved.Should().ContainSingle().Which.Should().BeSameAs(_record);
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Vehicle);
        entry.SubjectId.Should().Be(_record.Vehicle.Id);
        entry.Fields.Should().BeEquivalentTo("plate", "color");
    }

    [Fact]
    public async Task Handle_MovingTheVehicleToAnotherUnit_UsesTheNewUnitOnTheView_AndLogsTheUnit()
    {
        CondominiumUnit other = _kit.ActiveUnit("202", "Bloco B");

        Result<VehicleView> result = await Handler().Handle(Same() with { UnitId = other.Id }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnitId.Should().Be(other.Id);
        result.Value.UnitBlock.Should().Be("Bloco B");
        result.Value.UnitNumber.Should().Be("202");
        _kit.Logged.Single().Fields.Should().BeEquivalentTo("unit");
    }

    [Fact]
    public async Task Handle_MovingTheVehicleToAUnitThatDoesNotExist_FailsWithUnitNotFound()
    {
        Result<VehicleView> result = await Handler().Handle(Same() with { UnitId = Guid.CreateVersion7() }, default);

        result.Error.Should().Be(UnitErrors.NotFound);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MovingTheVehicleToADeactivatedUnit_FailsWithUnitDeactivated()
    {
        CondominiumUnit deactivated = _kit.DeactivatedUnit();

        (await Handler().Handle(Same() with { UnitId = deactivated.Id }, default)).Error.Should().Be(UnitErrors.Deactivated);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EditingAVehicleOfADeactivatedUnitWithoutMovingIt_Works()
    {
        CondominiumUnit deactivated = _kit.DeactivatedUnit("303");
        VehicleRecord record = _kit.Existing(deactivated, "DEF4567", "Gol", "Preto", version: "3");
        UpdateVehicleCommand command = new(_kit.ActorId, record.Vehicle.Id, "3", deactivated.Id, "DEF4567", "Gol", "Vermelho");

        Result<VehicleView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Color.Should().Be("Vermelho");
    }

    [Fact]
    public async Task Handle_WithAPlateThatBelongsToAnotherVehicle_FailsWithAConflict()
    {
        _kit.Vehicles.Setup(v => v.IsPlateTakenAsync("XYZ9876", _record.Vehicle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Result<VehicleView> result = await Handler().Handle(Same() with { Plate = "XYZ9876" }, default);

        result.Error.Should().Be(VehicleErrors.PlateAlreadyRegistered);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_KeepingThePlate_DoesNotAskTheDatabaseAboutIt()
    {
        await Handler().Handle(Same() with { Color = "Branco" }, default);

        _kit.Vehicles.Verify(v => v.IsPlateTakenAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheDatabaseReportsAConflict_ReturnsIt()
    {
        _kit.Vehicles
            .Setup(v => v.SaveAsync(It.IsAny<VehicleRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(CondominiumErrors.ConcurrencyConflict));

        (await Handler().Handle(Same() with { Model = "Outro" }, default)).Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
    }
}
