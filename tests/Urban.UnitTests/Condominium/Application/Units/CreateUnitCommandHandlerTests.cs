using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Units;

public class CreateUnitCommandHandlerTests
{
    private readonly UnitsTestKit _kit = new();

    private CreateUnitCommandHandler Handler() => new(_kit.Units.Object, _kit.Tenant, _kit.Atomic, _kit.Clock.Object);

    private CreateUnitCommand Valid() =>
        new(_kit.ActorId, "Bloco A", "101", UnitType.Apartment, 0.0125m, 85.5m);

    [Fact]
    public async Task Handle_CreatesTheUnitInTheCondominiumOfTheRequest_AndLogsOnlyTheFieldNames()
    {
        Result<UnitView> result = await Handler().Handle(Valid(), default);

        result.IsSuccess.Should().BeTrue();
        UnitView view = result.Value;
        view.Block.Should().Be("Bloco A");
        view.Number.Should().Be("101");
        view.Type.Should().Be(UnitType.Apartment);
        view.IdealFraction.Should().Be(0.0125m);
        view.Area.Should().Be(85.5m);
        view.IsActive.Should().BeTrue();
        view.Version.Should().Be("7");

        _kit.Added.Should().ContainSingle().Which.CondominiumId.Should().Be(_kit.CondominiumId);
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(_kit.ActorId);
        entry.Kind.Should().Be(ChangeKind.Unit);
        entry.SubjectId.Should().Be(view.Id);
        entry.At.Should().Be(UnitsTestKit.Now);
        entry.Fields.Should().BeEquivalentTo(["block", "number", "type", "idealFraction", "area"]);
        entry.Fields.Should().NotContain(["Bloco A", "101"]);
    }

    [Fact]
    public async Task Handle_LogsOnlyTheFieldsThatWereFilledIn()
    {
        CreateUnitCommand command = new(_kit.ActorId, null, "Casa 7", UnitType.House, null, null);

        await Handler().Handle(command, default);

        _kit.Logged.Single().Fields.Should().BeEquivalentTo(["number", "type"]);
    }

    [Fact]
    public async Task Handle_WithAnIdentifierThatAlreadyExists_FailsWithAConflict_AndSavesNothing()
    {
        _kit.Units
            .Setup(u => u.IsIdentityTakenAsync(new UnitIdentity("Bloco A", "101"), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<UnitView> result = await Handler().Handle(Valid(), default);

        result.Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
        _kit.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheFractionWouldPushTheSumPastOneHundredPercent_FailsAndSavesNothing()
    {
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(0.99m);

        Result<UnitView> result = await Handler().Handle(Valid(), default);

        result.Error.Should().Be(UnitErrors.FractionSumExceeded);
        _kit.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AllowsTheFractionThatMakesTheSumExactlyOneHundredPercent()
    {
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(0.9875m);

        Result<UnitView> result = await Handler().Handle(Valid(), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutAFraction_DoesNotEvenLookAtTheSum()
    {
        CreateUnitCommand command = new(_kit.ActorId, null, "101", UnitType.Apartment, null, null);

        await Handler().Handle(command, default);

        _kit.Units.Verify(u => u.ActiveFractionTotalAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAManagerWithoutACondominium_FailsWithCondominiumNotFound()
    {
        Result<UnitView> result = await new CreateUnitCommandHandler(_kit.Units.Object, _kit.WithoutATenant().Tenant, _kit.Atomic, _kit.Clock.Object)
            .Handle(Valid(), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
        _kit.Atomic.Runs.Should().Be(0);
    }

    [Theory]
    [InlineData("", UnitType.Apartment, 0.01, 50)]
    [InlineData("101", UnitType.Apartment, 1.5, 50)]
    [InlineData("101", UnitType.Apartment, 0.01, -3)]
    [InlineData("101", (UnitType)0, 0.01, 50)]
    public async Task Handle_WithInvalidData_FailsWithTheFieldErrorAndTouchesNothing(string number, UnitType type, double fraction, double area)
    {
        CreateUnitCommand command = new(_kit.ActorId, null, number, type, (decimal)fraction, (decimal)area);

        Result<UnitView> result = await Handler().Handle(command, default);

        result.IsFailure.Should().BeTrue();
        _kit.Added.Should().BeEmpty();
        _kit.Units.Verify(u => u.IsIdentityTakenAsync(It.IsAny<UnitIdentity>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RunsInsideTheAtomicWork_AndAFailedSaveIsReturnedSoEverythingIsUndone()
    {
        _kit.Units
            .Setup(u => u.AddAsync(It.IsAny<CondominiumUnit>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(UnitErrors.IdentifierAlreadyExists));

        Result<UnitView> result = await Handler().Handle(Valid(), default);

        result.Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
        _kit.Atomic.Runs.Should().Be(1);
        _kit.Atomic.Outcome!.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OnSuccess_TheAtomicWorkCommits()
    {
        await Handler().Handle(Valid(), default);

        _kit.Atomic.Runs.Should().Be(1);
        _kit.Atomic.Outcome!.IsSuccess.Should().BeTrue();
    }
}
