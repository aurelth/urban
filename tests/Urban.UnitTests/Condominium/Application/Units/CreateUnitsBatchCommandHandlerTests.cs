using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Units;

/// <summary>O cadastro em lote: até 500 unidades, tudo ou nada, com a posição e o código de cada problema.</summary>
public class CreateUnitsBatchCommandHandlerTests
{
    private readonly UnitsTestKit _kit = new();

    private CreateUnitsBatchCommandHandler Handler() => new(_kit.Units.Object, _kit.Tenant, _kit.Atomic, _kit.Clock.Object);

    private static UnitInput Apartment(string number, string? block = null, decimal? fraction = null, decimal? area = null) =>
        new(block, number, UnitType.Apartment, fraction, area);

    private CreateUnitsBatchCommand Batch(params UnitInput[] units) => new(_kit.ActorId, units);

    [Fact]
    public async Task Handle_CreatesEveryUnit_AndWritesASingleLogEntryWithTheCount()
    {
        UnitBatchResult result = await Handler().Handle(Batch(Apartment("301"), Apartment("302"), Apartment("401", fraction: 0.1m, area: 60m)), default);

        result.IsSuccess.Should().BeTrue();
        result.Created.Should().Be(3);
        IReadOnlyList<CondominiumUnit> created = _kit.AddedRanges.Should().ContainSingle().Subject;
        created.Select(u => u.Number).Should().Equal("301", "302", "401");
        created.Should().OnlyContain(u => u.CondominiumId == _kit.CondominiumId);
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.UnitBatch);
        entry.Fields.Should().Equal("count:3");
        entry.ActorUserId.Should().Be(_kit.ActorId);
        entry.SubjectId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_IsAllOrNothing_OneInvalidUnitUndoesTheBatchAndTheErrorCarriesThePositionAndTheCode()
    {
        UnitBatchResult result = await Handler().Handle(Batch(Apartment("301"), Apartment(""), Apartment("303", fraction: 5m)), default);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().BeEquivalentTo([new UnitBatchIssue(1, UnitErrors.NumberInvalid.Code), new UnitBatchIssue(2, UnitErrors.IdealFractionInvalid.Code)]);
        result.Rejection.Should().Be(UnitErrors.NumberInvalid);
        _kit.AddedRanges.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithRepeatedIdentifiersInsideTheBatch_FailsMarkingEveryPositionInvolved_IgnoringCase()
    {
        UnitBatchResult result = await Handler().Handle(
            Batch(Apartment("101", "Bloco A"), Apartment("102", "Bloco A"), Apartment("101", "bloco a"), Apartment("101", "Bloco B")),
            default);

        result.Rejection.Should().Be(UnitErrors.IdentifierAlreadyExists);
        result.Issues.Should().BeEquivalentTo([
            new UnitBatchIssue(0, UnitErrors.IdentifierAlreadyExists.Code),
            new UnitBatchIssue(2, UnitErrors.IdentifierAlreadyExists.Code)]);
        _kit.AddedRanges.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAnIdentifierThatAlreadyExists_FailsWithItsPosition()
    {
        _kit.Units
            .Setup(u => u.TakenAmongAsync(It.IsAny<IReadOnlyCollection<UnitIdentity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<UnitIdentity> { new(null, "302") });

        UnitBatchResult result = await Handler().Handle(Batch(Apartment("301"), Apartment("302"), Apartment("303")), default);

        result.Rejection.Should().Be(UnitErrors.IdentifierAlreadyExists);
        result.Issues.Should().Equal(new UnitBatchIssue(1, UnitErrors.IdentifierAlreadyExists.Code));
        _kit.AddedRanges.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheExistingFractionsPlusTheBatchPassOneHundredPercent_Refuses()
    {
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(0.7m);

        UnitBatchResult result = await Handler().Handle(Batch(Apartment("1", fraction: 0.2m), Apartment("2", fraction: 0.2m)), default);

        result.Rejection.Should().Be(UnitErrors.FractionSumExceeded);
        result.Issues.Should().BeEmpty();
        _kit.AddedRanges.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheSumIsExactlyOneHundredPercent_Accepts()
    {
        _kit.Units.Setup(u => u.ActiveFractionTotalAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(0.6m);

        UnitBatchResult result = await Handler().Handle(Batch(Apartment("1", fraction: 0.2m), Apartment("2", fraction: 0.2m)), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithAnEmptyBatch_FailsWithTheSizeError()
    {
        UnitBatchResult result = await Handler().Handle(Batch(), default);

        result.Rejection.Should().Be(UnitErrors.BatchSizeInvalid);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithMoreThanFiveHundredUnits_FailsWithTheSizeError_AndExactlyFiveHundredPasses()
    {
        UnitInput[] tooMany = [.. Enumerable.Range(1, 501).Select(i => Apartment($"{i}"))];
        UnitInput[] limit = [.. Enumerable.Range(1, 500).Select(i => Apartment($"{i}"))];

        (await Handler().Handle(Batch(tooMany), default)).Rejection.Should().Be(UnitErrors.BatchSizeInvalid);
        (await Handler().Handle(Batch(limit), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ARejectedSaveUndoesTheBatch_AndTheRejectionIsReturned()
    {
        _kit.Units
            .Setup(u => u.AddRangeAsync(It.IsAny<IReadOnlyList<CondominiumUnit>>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(UnitErrors.IdentifierAlreadyExists));

        UnitBatchResult result = await Handler().Handle(Batch(Apartment("1")), default);

        result.Rejection.Should().Be(UnitErrors.IdentifierAlreadyExists);
        result.Created.Should().Be(0);
        _kit.Atomic.Outcome!.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        UnitBatchResult result = await new CreateUnitsBatchCommandHandler(without.Units.Object, without.Tenant, without.Atomic, without.Clock.Object)
            .Handle(new CreateUnitsBatchCommand(without.ActorId, [Apartment("1")]), default);

        result.Rejection.Should().Be(CondominiumErrors.NotFound);
    }
}
