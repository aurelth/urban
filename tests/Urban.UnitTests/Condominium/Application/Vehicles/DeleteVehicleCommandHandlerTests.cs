using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Vehicles;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Vehicles;

public class DeleteVehicleCommandHandlerTests
{
    private readonly VehiclesTestKit _kit = new();

    private DeleteVehicleCommandHandler Handler() => new(_kit.Vehicles.Object, _kit.Tenant, _kit.Clock.Object);

    [Fact]
    public async Task Handle_RemovesTheVehicle_AndLogsTheRemovalWithoutValues()
    {
        CondominiumUnit unit = _kit.ActiveUnit();
        VehicleRecord record = _kit.Existing(unit);

        Result result = await Handler().Handle(new DeleteVehicleCommand(_kit.ActorId, record.Vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Deleted.Should().ContainSingle().Which.Should().BeSameAs(record);
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Vehicle);
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(_kit.ActorId);
        entry.SubjectId.Should().Be(record.Vehicle.Id);
        entry.Fields.Should().BeEquivalentTo("removed");
    }

    [Fact]
    public async Task Handle_WithAVehicleThatDoesNotExist_FailsWithNotFound()
    {
        Result result = await Handler().Handle(new DeleteVehicleCommand(_kit.ActorId, Guid.CreateVersion7()), default);

        result.Error.Should().Be(VehicleErrors.NotFound);
        _kit.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutACondominium_FailsWithNotFound()
    {
        _kit.WithoutATenant();

        (await Handler().Handle(new DeleteVehicleCommand(_kit.ActorId, Guid.CreateVersion7()), default)).Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenTheVehicleChangedInTheMeantime_ReturnsTheConflict()
    {
        CondominiumUnit unit = _kit.ActiveUnit();
        VehicleRecord record = _kit.Existing(unit);
        _kit.Vehicles
            .Setup(v => v.DeleteAsync(record, It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CondominiumErrors.ConcurrencyConflict));

        (await Handler().Handle(new DeleteVehicleCommand(_kit.ActorId, record.Vehicle.Id), default)).Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
    }
}
