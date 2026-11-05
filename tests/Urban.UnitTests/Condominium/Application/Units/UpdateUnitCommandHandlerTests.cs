using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Units;

public class UpdateUnitCommandHandlerTests
{
    private readonly UnitsTestKit _kit = new();
    private readonly UnitRecord _record;

    public UpdateUnitCommandHandlerTests()
    {
        _record = _kit.Existing(_kit.NewUnit("101", "Bloco A", idealFraction: 0.1m, area: 50m), version: "5");
    }

    private UpdateUnitCommandHandler Handler() => new(_kit.Units.Object, _kit.Tenant, _kit.Atomic, _kit.Clock.Object);

    /// <summary>A mesma unidade, sem nenhuma mudança: cada teste muda só o campo que quer provar.</summary>
    private UpdateUnitCommand Same(string? version = "5") =>
        new(_kit.ActorId, _record.Unit.Id, version, "Bloco A", "101", UnitType.Apartment, 0.1m, 50m);

    [Fact]
    public async Task Handle_WithoutTheVersion_FailsWithVersionRequired()
    {
        (await Handler().Handle(Same(version: null), default)).Error.Should().Be(UnitErrors.VersionRequired);
        (await Handler().Handle(Same(version: "  "), default)).Error.Should().Be(UnitErrors.VersionRequired);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAnOldVersion_FailsWithAConcurrencyConflict_AndChangesNothing()
    {
        Result<UnitView> result = await Handler().Handle(Same(version: "4") with { Number = "999" }, default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _record.Unit.Number.Should().Be("101");
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ChangesTheFieldsAndReturnsTheNewVersion()
    {
        UpdateUnitCommand command = Same() with { Block = "Bloco B", Number = " 202 ", Type = UnitType.House, IdealFraction = 0.12m, Area = 61.5m };

        Result<UnitView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Block.Should().Be("Bloco B");
        result.Value.Number.Should().Be("202");
        result.Value.Type.Should().Be(UnitType.House);
        result.Value.IdealFraction.Should().Be(0.12m);
        result.Value.Area.Should().Be(61.5m);
        result.Value.Version.Should().Be("8");
        _kit.Saved.Should().ContainSingle().Which.Should().BeSameAs(_record);
    }

    [Fact]
    public async Task Handle_WithAnIdentifierThatBelongsToAnotherUnit_FailsWithAConflict()
    {
        _kit.Units
            .Setup(u => u.IsIdentityTakenAsync(new UnitIdentity("Bloco A", "102"), _record.Unit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<UnitView> result = await Handler().Handle(Same() with { Number = "102" }, default);

        result.Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_KeepingTheOwnIdentifier_PassesBecauseTheStoreIsToldToIgnoreThisUnit()
    {
        UpdateUnitCommand command = Same() with { Area = 55m };

        Result<UnitView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _kit.Units.Verify(u => u.IsIdentityTakenAsync(It.IsAny<UnitIdentity>(), _record.Unit.Id, It.IsAny<CancellationToken>()), Times.AtMostOnce);
    }

    [Fact]
    public async Task Handle_CountsTheSumWithoutTheOldFractionOfTheSameUnit()
    {
        // As outras unidades somam 85%: trocar 10% por 12% fecha em 97% e passa.
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(_record.Unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0.85m);

        Result<UnitView> result = await Handler().Handle(Same() with { IdealFraction = 0.12m }, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheNewFractionPushesTheSumPastOneHundredPercent_FailsAndChangesNothing()
    {
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(_record.Unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0.95m);

        Result<UnitView> result = await Handler().Handle(Same() with { IdealFraction = 0.06m }, default);

        result.Error.Should().Be(UnitErrors.FractionSumExceeded);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LogsOnlyTheNamesOfTheFieldsThatChanged()
    {
        await Handler().Handle(Same() with { Area = 55m, Block = "Bloco B" }, default);

        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Unit);
        entry.SubjectId.Should().Be(_record.Unit.Id);
        entry.ActorUserId.Should().Be(_kit.ActorId);
        entry.Fields.Should().BeEquivalentTo(["block", "area"]);
    }

    [Fact]
    public async Task Handle_WhenNothingChanged_SucceedsWithoutSavingAndWithoutChangingTheVersion()
    {
        Result<UnitView> result = await Handler().Handle(Same(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("5");
        _kit.Saved.Should().BeEmpty();
        _kit.Atomic.Runs.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenOnlyTheSpacingOfTheTextDiffers_NothingChanged()
    {
        Result<UnitView> result = await Handler().Handle(Same() with { Number = "  101 ", Block = " Bloco   A" }, default);

        result.Value.Version.Should().Be("5");
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OfADeactivatedUnit_FailsWithDeactivated()
    {
        CondominiumUnit gone = _kit.NewUnit("303");
        gone.Deactivate(UnitsTestKit.Now);
        UnitRecord record = _kit.Existing(gone, "2");

        Result<UnitView> result = await Handler().Handle(new UpdateUnitCommand(_kit.ActorId, gone.Id, "2", null, "304", UnitType.Apartment, null, null), default);

        result.Error.Should().Be(UnitErrors.Deactivated);
        record.Unit.Number.Should().Be("303");
    }

    [Fact]
    public async Task Handle_WithInvalidData_FailsWithTheFieldErrorAndSavesNothing()
    {
        Result<UnitView> result = await Handler().Handle(Same() with { IdealFraction = 3m }, default);

        result.Error.Should().Be(UnitErrors.IdealFractionInvalid);
        _record.Unit.IdealFraction.Should().Be(0.1m);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OfAUnitThatTheStoreDoesNotSee_FailsWithNotFound()
    {
        _kit.Units.Setup(u => u.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UnitRecord?)null);

        Result<UnitView> result = await Handler().Handle(Same() with { UnitId = Guid.CreateVersion7() }, default);

        result.Error.Should().Be(UnitErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        Result<UnitView> result = await new UpdateUnitCommandHandler(without.Units.Object, without.Tenant, without.Atomic, without.Clock.Object)
            .Handle(Same(), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Handle_RunsTheSaveInsideTheAtomicWork_AndAFailedSaveIsReturned()
    {
        _kit.Units
            .Setup(u => u.SaveAsync(It.IsAny<UnitRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(CondominiumErrors.ConcurrencyConflict));

        Result<UnitView> result = await Handler().Handle(Same() with { Area = 99m }, default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _kit.Atomic.Outcome!.IsFailure.Should().BeTrue();
    }
}
