using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

public class EndOccupancyCommandHandlerTests
{
    private readonly ResidentsTestKit _kit = new();

    private EndOccupancyCommandHandler Handler() => new(_kit.Residents.Object, _kit.Tenant, _kit.Clock.Object, _kit.Views);

    private static EndOccupancyCommand Command(Guid residentId, Guid occupancyId, DateOnly? end = null) =>
        new(Guid.CreateVersion7(), residentId, occupancyId, end ?? new DateOnly(2026, 10, 31));

    [Fact]
    public async Task Handle_FillsTheEndDate_LogsOnlyTheFieldName_AndReturnsTheResident()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        Occupancy occupancy = record.Occupancies.Single().Occupancy;
        EndOccupancyCommand command = Command(record.Person.Id, occupancy.Id, new DateOnly(2026, 10, 31));

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(record.Person.Id);
        _kit.OccupanciesEnded.Should().ContainSingle().Which.Should().BeSameAs(occupancy);
        occupancy.EndDate.Should().Be(new DateOnly(2026, 10, 31));
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(command.ActorUserId);
        entry.Kind.Should().Be(ChangeKind.Resident);
        entry.SubjectId.Should().Be(record.Person.Id);
        entry.At.Should().Be(ResidentsTestKit.Now);
        entry.Fields.Should().Equal("occupancyEnd");
    }

    [Fact]
    public async Task Handle_WithoutACondominium_Fails()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        _kit.WithoutATenant();

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, record.Occupancies.Single().Occupancy.Id), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Handle_OfAnUnknownResident_FailsAsNotFound()
    {
        _kit.Residents.Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ResidentRecord?)null);

        Result<ResidentView> result = await Handler().Handle(Command(Guid.CreateVersion7(), Guid.CreateVersion7()), default);

        result.Error.Should().Be(ResidentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_OfAnOccupancyThatIsNotOfThisResident_FailsAsNotFound_AndSavesNothing()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, Guid.CreateVersion7()), default);

        result.Error.Should().Be(OccupancyErrors.NotFound);
        _kit.OccupanciesEnded.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OfAnOccupancyThatAlreadyEnded_Fails()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        Occupancy occupancy = record.Occupancies.Single().Occupancy;
        occupancy.MoveOut(new DateOnly(2026, 1, 31), ResidentBuilders.Today);

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, occupancy.Id), default);

        result.Error.Should().Be(OccupancyErrors.AlreadyEnded);
        _kit.OccupanciesEnded.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutAnEndDate_Fails()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        EndOccupancyCommand command = Command(record.Person.Id, record.Occupancies.Single().Occupancy.Id) with { EndDate = null };

        (await Handler().Handle(command, default)).Error.Should().Be(OccupancyErrors.EndDateInvalid);
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2026, 11, 14)]
    public async Task Handle_BeforeTheStartOrAfterToday_Fails_AndKeepsTheOccupancyActive(int year, int month, int day)
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        Occupancy occupancy = record.Occupancies.Single().Occupancy;

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, occupancy.Id, new DateOnly(year, month, day)), default);

        result.Error.Should().Be(OccupancyErrors.EndDateInvalid);
        occupancy.IsActive.Should().BeTrue();
        _kit.OccupanciesEnded.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TodayIsInBrazilTime()
    {
        // 02:00 UTC de 14/11 ainda é 13/11 no Brasil: sair "em 14/11" é futuro.
        _kit.Clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2026, 11, 14, 2, 0, 0, TimeSpan.Zero));
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        Guid occupancyId = record.Occupancies.Single().Occupancy.Id;

        (await Handler().Handle(Command(record.Person.Id, occupancyId, new DateOnly(2026, 11, 14)), default)).Error.Should().Be(OccupancyErrors.EndDateInvalid);
        (await Handler().Handle(Command(record.Person.Id, occupancyId, new DateOnly(2026, 11, 13)), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TheStoreErrorComesBackAsItIs_ForTwoRequestsAtTheSameTime()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        _kit.Residents
            .Setup(r => r.EndOccupancyAsync(It.IsAny<Occupancy>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OccupancyErrors.AlreadyEnded));

        Result<ResidentView> result = await Handler().Handle(Command(record.Person.Id, record.Occupancies.Single().Occupancy.Id), default);

        result.Error.Should().Be(OccupancyErrors.AlreadyEnded);
    }
}
