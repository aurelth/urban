using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

public class AddOccupancyCommandHandlerTests
{
    private readonly ResidentsTestKit _kit = new();

    private AddOccupancyCommandHandler Handler() => new(_kit.Residents.Object, _kit.Units.Object, _kit.Tenant, _kit.Clock.Object, _kit.Views);

    private CondominiumUnit OtherUnit()
    {
        CondominiumUnit other = UnitBuilders.Unit(_kit.CondominiumId, "Bloco B", "202");
        _kit.Units.Setup(u => u.FindAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new UnitRecord(other, "1"));

        return other;
    }

    private static AddOccupancyCommand Command(Guid residentId, Guid unitId, OccupancyKind kind = OccupancyKind.Tenant, DateOnly? start = null) =>
        new(Guid.CreateVersion7(), residentId, unitId, kind, start ?? new DateOnly(2025, 6, 1));

    [Fact]
    public async Task Handle_AddsTheOccupancy_LogsOnlyTheFieldName_AndReturnsTheResidentAsTheStoreReadsItNow()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        CondominiumUnit other = OtherUnit();
        AddOccupancyCommand command = Command(record.Person.Id, other.Id, OccupancyKind.Tenant, new DateOnly(2025, 6, 1));

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(record.Person.Id);
        Occupancy added = _kit.OccupanciesAdded.Should().ContainSingle().Subject;
        added.PersonId.Should().Be(record.Person.Id);
        added.CondominiumId.Should().Be(_kit.CondominiumId);
        added.UnitId.Should().Be(other.Id);
        added.Kind.Should().Be(OccupancyKind.Tenant);
        added.StartDate.Should().Be(new DateOnly(2025, 6, 1));
        added.EndDate.Should().BeNull();
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(command.ActorUserId);
        entry.Kind.Should().Be(ChangeKind.Resident);
        entry.SubjectId.Should().Be(record.Person.Id);
        entry.Fields.Should().Equal("occupancy");
        _kit.Residents.Verify(r => r.FindAsync(record.Person.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_AllowsReturningToAUnitTheResidentAlreadyLeft()
    {
        Person person = _kit.NewResident();
        Occupancy old = ResidentBuilders.Occupancy(person, _kit.Unit.Id, OccupancyKind.Owner, new DateOnly(2020, 1, 1));
        old.MoveOut(new DateOnly(2022, 1, 1), ResidentBuilders.Today);
        _kit.Existing(person, new ResidentOccupancy(old, _kit.Unit.Block, _kit.Unit.Number));

        Result<ResidentView> result = await Handler().Handle(Command(person.Id, _kit.Unit.Id), default);

        result.IsSuccess.Should().BeTrue();
        _kit.OccupanciesAdded.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenTheResidentAlreadyLivesInTheUnit_FailsAndNeverSaves()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, _kit.Unit.Id), default);

        result.Error.Should().Be(OccupancyErrors.AlreadyActive);
        _kit.OccupanciesAdded.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutACondominium_Fails()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        _kit.WithoutATenant();

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, OtherUnit().Id), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Handle_OfAnUnknownResident_FailsAsNotFound_BeforeLookingAtTheUnit()
    {
        _kit.Residents.Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ResidentRecord?)null);

        Result<ResidentView> result = await Handler().Handle(Command(Guid.CreateVersion7(), _kit.Unit.Id), default);

        result.Error.Should().Be(ResidentErrors.NotFound);
        _kit.Units.Verify(u => u.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnUnknownUnit_FailsAsNotFound()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        Guid unknown = Guid.CreateVersion7();
        _kit.Units.Setup(u => u.FindAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((UnitRecord?)null);

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, unknown), default);

        result.Error.Should().Be(UnitErrors.NotFound);
        _kit.OccupanciesAdded.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithADeactivatedUnit_Fails()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        CondominiumUnit deactivated = UnitBuilders.Deactivated(_kit.CondominiumId, "303");
        _kit.Units.Setup(u => u.FindAsync(deactivated.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new UnitRecord(deactivated, "2"));

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, deactivated.Id), default);

        result.Error.Should().Be(UnitErrors.Deactivated);
    }

    [Theory]
    [InlineData(0, 2025, "condominium.occupancy.kind_invalid")]
    [InlineData(1, 1800, "condominium.occupancy.start_date_invalid")]
    [InlineData(1, 2999, "condominium.occupancy.start_date_invalid")]
    public async Task Handle_WithInvalidData_FailsWithTheCodeOfTheField(int kind, int year, string code)
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        AddOccupancyCommand command = Command(record.Person.Id, OtherUnit().Id, (OccupancyKind)kind, new DateOnly(year, 6, 1));

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.Error.Code.Should().Be(code);
        _kit.OccupanciesAdded.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutAStartDate_Fails()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        AddOccupancyCommand command = Command(record.Person.Id, OtherUnit().Id) with { StartDate = null };

        (await Handler().Handle(command, default)).Error.Should().Be(OccupancyErrors.StartDateInvalid);
    }

    [Fact]
    public async Task Handle_TheStoreErrorComesBackAsItIs_ForTwoRequestsAtTheSameTime()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        _kit.Residents
            .Setup(r => r.AddOccupancyAsync(It.IsAny<Occupancy>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OccupancyErrors.AlreadyActive));

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, OtherUnit().Id), default);

        result.Error.Should().Be(OccupancyErrors.AlreadyActive);
    }
}
