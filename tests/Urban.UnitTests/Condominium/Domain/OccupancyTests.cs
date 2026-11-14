using FluentAssertions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class OccupancyTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid PersonId = Guid.CreateVersion7();
    private static readonly Guid UnitId = Guid.CreateVersion7();

    private static Result<Occupancy> Start(
        Guid? condominiumId = null,
        Guid? personId = null,
        Guid? unitId = null,
        OccupancyKind kind = OccupancyKind.Tenant,
        DateOnly? startDate = null) =>
        Occupancy.Start(condominiumId ?? CondominiumId, personId ?? PersonId, unitId ?? UnitId, kind, startDate ?? new DateOnly(2025, 1, 10), Today, Now);

    [Fact]
    public void Start_WithValidData_CreatesAnActiveOccupancy()
    {
        Result<Occupancy> result = Start();

        result.IsSuccess.Should().BeTrue();
        Occupancy occupancy = result.Value;
        occupancy.Id.Should().NotBeEmpty();
        occupancy.CondominiumId.Should().Be(CondominiumId);
        occupancy.PersonId.Should().Be(PersonId);
        occupancy.UnitId.Should().Be(UnitId);
        occupancy.Kind.Should().Be(OccupancyKind.Tenant);
        occupancy.StartDate.Should().Be(new DateOnly(2025, 1, 10));
        occupancy.EndDate.Should().BeNull();
        occupancy.IsActive.Should().BeTrue();
        occupancy.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Start_GivesEachOccupancyItsOwnId()
    {
        Start().Value.Id.Should().NotBe(Start().Value.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Start_WithAnUnknownKind_Fails(int kind)
    {
        Start(kind: (OccupancyKind)kind).Error.Should().Be(OccupancyErrors.KindInvalid);
        OccupancyErrors.KindInvalid.Code.Should().Be("condominium.occupancy.kind_invalid");
    }

    [Fact]
    public void Start_WithoutAStartDate_Fails()
    {
        Result<Occupancy> result = Occupancy.Start(CondominiumId, PersonId, UnitId, OccupancyKind.Owner, null, Today, Now);

        result.Error.Should().Be(OccupancyErrors.StartDateInvalid);
        OccupancyErrors.StartDateInvalid.Code.Should().Be("condominium.occupancy.start_date_invalid");
    }

    [Fact]
    public void Start_BoundariesOfTheStartDate()
    {
        Start(startDate: new DateOnly(1900, 1, 1)).IsSuccess.Should().BeTrue();
        Start(startDate: new DateOnly(1899, 12, 31)).Error.Should().Be(OccupancyErrors.StartDateInvalid);
        Start(startDate: Today.AddYears(1)).IsSuccess.Should().BeTrue();
        Start(startDate: Today.AddYears(1).AddDays(1)).Error.Should().Be(OccupancyErrors.StartDateInvalid);
    }

    [Fact]
    public void Start_WithAnEmptyId_FailsWithTheMatchingError()
    {
        Start(condominiumId: Guid.Empty).Error.Should().Be(OccupancyErrors.CondominiumRequired);
        Start(personId: Guid.Empty).Error.Should().Be(OccupancyErrors.PersonRequired);
        Start(unitId: Guid.Empty).Error.Should().Be(OccupancyErrors.UnitRequired);
        OccupancyErrors.CondominiumRequired.Code.Should().Be("condominium.occupancy.condominium_required");
        OccupancyErrors.PersonRequired.Code.Should().Be("condominium.occupancy.person_required");
        OccupancyErrors.UnitRequired.Code.Should().Be("condominium.occupancy.unit_required");
    }

    [Fact]
    public void Start_AsADependent_CreatesTheOccupancy()
    {
        Result<Occupancy> result = Start(kind: OccupancyKind.Dependent);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(OccupancyKind.Dependent);
        ((int)OccupancyKind.Dependent).Should().Be(3);
    }

    [Fact]
    public void UnderAge_ExplainsThatOnlyDependentsCanBeMinors()
    {
        ResidentErrors.UnderAge.Code.Should().Be("condominium.resident.under_age");
        ResidentErrors.UnderAge.Description.Should().Be("Quem tem menos de 18 anos só pode ser cadastrado como dependente");
    }
}
