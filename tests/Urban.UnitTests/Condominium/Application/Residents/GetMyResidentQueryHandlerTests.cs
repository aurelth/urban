using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

public class GetMyResidentQueryHandlerTests
{
    private readonly ResidentsTestKit _kit = new();

    private GetMyResidentQueryHandler Handler() => new(_kit.Residents.Object, _kit.Tenant, _kit.Views);

    private void Found(Guid userId, ResidentRecord record) =>
        _kit.Residents.Setup(r => r.FindByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(record);

    [Fact]
    public async Task Handle_ReturnsTheRegistrationOfTheAccountWithItsUnits()
    {
        Person person = _kit.WithAccount(_kit.NewResident("Maria Souza"), ResidentAccessStatus.Active);
        Occupancy occupancy = ResidentBuilders.Occupancy(person, _kit.Unit.Id, OccupancyKind.Owner, new DateOnly(2024, 3, 1));
        Found(person.UserId!.Value, new ResidentRecord(person, [new ResidentOccupancy(occupancy, _kit.Unit.Block, _kit.Unit.Number)], "4"));

        Result<ResidentView> result = await Handler().Handle(new GetMyResidentQuery(person.UserId.Value), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(person.Id);
        result.Value.FullName.Should().Be("Maria Souza");
        result.Value.MaskedCpf.Should().Be(person.Cpf.Masked);
        result.Value.AccessStatus.Should().Be(ResidentAccessStatus.Active);
        ResidentOccupancyView unit = result.Value.Occupancies.Should().ContainSingle().Subject;
        unit.UnitNumber.Should().Be(_kit.Unit.Number);
        unit.Kind.Should().Be(OccupancyKind.Owner);
    }

    [Fact]
    public async Task Handle_ForTheManagerWithoutAnyUnit_ReturnsHisPersonWithNoOccupancies()
    {
        Guid userId = Guid.CreateVersion7();
        Person manager = CondominiumBuilders.Manager(_kit.CondominiumId, userId);
        Found(userId, new ResidentRecord(manager, [], "7"));

        Result<ResidentView> result = await Handler().Handle(new GetMyResidentQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(manager.Id);
        result.Value.Occupancies.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutAPersonForTheAccount_IsNotFound()
    {
        Result<ResidentView> result = await Handler().Handle(new GetMyResidentQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(ResidentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WithoutACondominiumInTheRequest_FailsAndDoesNotRead()
    {
        _kit.WithoutATenant();

        Result<ResidentView> result = await Handler().Handle(new GetMyResidentQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
        _kit.Residents.Verify(r => r.FindByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
