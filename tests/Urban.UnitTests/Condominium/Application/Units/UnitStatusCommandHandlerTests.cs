using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Units;

/// <summary>Desativar e reativar: a unidade nunca é apagada, porque morador e cobrança vão apontar para ela.</summary>
public class UnitStatusCommandHandlerTests
{
    private readonly UnitsTestKit _kit = new();

    private DeactivateUnitCommandHandler Deactivator() => new(_kit.Units.Object, _kit.Tenant, _kit.Atomic, _kit.Clock.Object);

    private ReactivateUnitCommandHandler Reactivator() => new(_kit.Units.Object, _kit.Tenant, _kit.Atomic, _kit.Clock.Object);

    private UnitRecord ExistingDeactivated(decimal? fraction = null, string version = "5")
    {
        CondominiumUnit unit = _kit.NewUnit("101", idealFraction: fraction);
        unit.Deactivate(UnitsTestKit.Now.AddDays(-1));

        return _kit.Existing(unit, version);
    }

    // ---- Desativar ----

    [Fact]
    public async Task Deactivate_DeactivatesTheUnitStoresTheDateAndLogsIt()
    {
        UnitRecord record = _kit.Existing(_kit.NewUnit("101"), "5");

        Result<UnitView> result = await Deactivator().Handle(new DeactivateUnitCommand(_kit.ActorId, record.Unit.Id, "5"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        result.Value.DeactivatedAt.Should().Be(UnitsTestKit.Now);
        result.Value.Version.Should().Be("8");
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Unit);
        entry.SubjectId.Should().Be(record.Unit.Id);
        entry.Fields.Should().Equal("deactivated");
    }

    [Fact]
    public async Task Deactivate_WithoutTheVersion_FailsWithVersionRequired()
    {
        UnitRecord record = _kit.Existing(_kit.NewUnit(), "5");

        (await Deactivator().Handle(new DeactivateUnitCommand(_kit.ActorId, record.Unit.Id, null), default)).Error.Should().Be(UnitErrors.VersionRequired);
    }

    [Fact]
    public async Task Deactivate_WithAnOldVersion_FailsWithAConcurrencyConflict_AndTheUnitStaysActive()
    {
        UnitRecord record = _kit.Existing(_kit.NewUnit(), "5");

        Result<UnitView> result = await Deactivator().Handle(new DeactivateUnitCommand(_kit.ActorId, record.Unit.Id, "4"), default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        record.Unit.IsActive.Should().BeTrue();
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Deactivate_AnAlreadyDeactivatedUnit_FailsAndSavesNothing()
    {
        UnitRecord record = ExistingDeactivated();

        Result<UnitView> result = await Deactivator().Handle(new DeactivateUnitCommand(_kit.ActorId, record.Unit.Id, "5"), default);

        result.Error.Should().Be(UnitErrors.AlreadyDeactivated);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Deactivate_OfAnUnknownUnit_FailsWithNotFound()
    {
        _kit.Units.Setup(u => u.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UnitRecord?)null);

        (await Deactivator().Handle(new DeactivateUnitCommand(_kit.ActorId, Guid.CreateVersion7(), "5"), default)).Error.Should().Be(UnitErrors.NotFound);
    }

    [Fact]
    public async Task Deactivate_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        Result<UnitView> result = await new DeactivateUnitCommandHandler(without.Units.Object, without.Tenant, without.Atomic, without.Clock.Object)
            .Handle(new DeactivateUnitCommand(without.ActorId, Guid.CreateVersion7(), "5"), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    // ---- Reativar ----

    [Fact]
    public async Task Reactivate_ReactivatesTheUnitAndLogsIt()
    {
        UnitRecord record = ExistingDeactivated();

        Result<UnitView> result = await Reactivator().Handle(new ReactivateUnitCommand(_kit.ActorId, record.Unit.Id, "5"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.DeactivatedAt.Should().BeNull();
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Fields.Should().Equal("reactivated");
        entry.Kind.Should().Be(ChangeKind.Unit);
        entry.SubjectId.Should().Be(record.Unit.Id);
        entry.At.Should().Be(UnitsTestKit.Now);
    }

    [Fact]
    public async Task Reactivate_AnActiveUnit_Fails()
    {
        UnitRecord record = _kit.Existing(_kit.NewUnit(), "5");

        (await Reactivator().Handle(new ReactivateUnitCommand(_kit.ActorId, record.Unit.Id, "5"), default)).Error.Should().Be(UnitErrors.AlreadyActive);
    }

    [Fact]
    public async Task Reactivate_IsRefusedWhenTheFractionOfTheUnitWouldPushTheSumPastOneHundredPercent()
    {
        UnitRecord record = ExistingDeactivated(fraction: 0.2m);
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(record.Unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0.9m);

        Result<UnitView> result = await Reactivator().Handle(new ReactivateUnitCommand(_kit.ActorId, record.Unit.Id, "5"), default);

        result.Error.Should().Be(UnitErrors.FractionSumExceeded);
        record.Unit.IsActive.Should().BeFalse();
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Reactivate_IsAllowedWhenTheSumStaysWithinOneHundredPercent()
    {
        UnitRecord record = ExistingDeactivated(fraction: 0.2m);
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(record.Unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0.8m);

        (await Reactivator().Handle(new ReactivateUnitCommand(_kit.ActorId, record.Unit.Id, "5"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_WithoutTheVersion_FailsWithVersionRequired_AndWithAnOldOneWithAConflict()
    {
        UnitRecord record = ExistingDeactivated();

        (await Reactivator().Handle(new ReactivateUnitCommand(_kit.ActorId, record.Unit.Id, null), default)).Error.Should().Be(UnitErrors.VersionRequired);
        (await Reactivator().Handle(new ReactivateUnitCommand(_kit.ActorId, record.Unit.Id, "4"), default)).Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
    }

    [Fact]
    public async Task Reactivate_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        Result<UnitView> result = await new ReactivateUnitCommandHandler(without.Units.Object, without.Tenant, without.Atomic, without.Clock.Object)
            .Handle(new ReactivateUnitCommand(without.ActorId, Guid.CreateVersion7(), "5"), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }
}
