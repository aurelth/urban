using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

/// <summary>
/// Tudo o que um handler de moradores precisa, já ligado: o condomínio da requisição, o relógio, a porta de moradores (que por
/// padrão grava com sucesso) e a de unidades (que por padrão acha uma unidade ativa). Cada teste muda só o que quer provar.
/// </summary>
internal sealed class ResidentsTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    public Mock<IResidents> Residents { get; } = new();

    public Mock<IUnits> Units { get; } = new();

    public Mock<IClock> Clock { get; } = new();

    public ITenantContext Tenant { get; private set; }

    public CondominiumUnit Unit { get; }

    public List<Person> AddedPeople { get; } = [];

    public List<Occupancy> AddedOccupancies { get; } = [];

    public List<ChangeLogEntry> Logged { get; } = [];

    public List<ResidentRecord> Saved { get; } = [];

    public List<Occupancy> OccupanciesAdded { get; } = [];

    public List<Occupancy> OccupanciesEnded { get; } = [];

    public ResidentsTestKit()
    {
        Tenant = new FixedTenant(CondominiumId);
        Clock.SetupGet(c => c.UtcNow).Returns(Now);

        Unit = UnitBuilders.Unit(CondominiumId, "Bloco A", "101");
        Units.Setup(u => u.FindAsync(Unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new UnitRecord(Unit, "1"));

        Residents.Setup(r => r.AddAsync(It.IsAny<Person>(), It.IsAny<Occupancy>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<Person, Occupancy, ChangeLogEntry, CancellationToken>((person, occupancy, entry, _) =>
            {
                AddedPeople.Add(person);
                AddedOccupancies.Add(occupancy);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success("9"));

        Residents.Setup(r => r.SaveAsync(It.IsAny<ResidentRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<ResidentRecord, ChangeLogEntry, CancellationToken>((record, entry, _) =>
            {
                Saved.Add(record);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success("10"));

        Residents.Setup(r => r.AddOccupancyAsync(It.IsAny<Occupancy>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<Occupancy, ChangeLogEntry, CancellationToken>((occupancy, entry, _) =>
            {
                OccupanciesAdded.Add(occupancy);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success());

        Residents.Setup(r => r.EndOccupancyAsync(It.IsAny<Occupancy>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<Occupancy, ChangeLogEntry, CancellationToken>((occupancy, entry, _) =>
            {
                OccupanciesEnded.Add(occupancy);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success());
    }

    public ResidentsTestKit WithoutATenant()
    {
        Tenant = new FixedTenant(null);

        return this;
    }

    /// <summary>Um morador "que já está no banco".</summary>
    public ResidentRecord Existing(Person person, params ResidentOccupancy[] occupancies)
    {
        ResidentRecord record = new(person, occupancies, "4");
        Residents.Setup(r => r.FindAsync(person.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        return record;
    }

    /// <summary>Um morador "que já está no banco" com um vínculo em andamento com a unidade do kit.</summary>
    public ResidentRecord ExistingWithActiveOccupancy(Person? person = null, OccupancyKind kind = OccupancyKind.Owner)
    {
        Person resident = person ?? NewResident();
        Occupancy occupancy = ResidentBuilders.Occupancy(resident, Unit.Id, kind, new DateOnly(2024, 3, 1));

        return Existing(resident, new ResidentOccupancy(occupancy, Unit.Block, Unit.Number));
    }

    public Person NewResident(string name = "Maria Souza", string? email = "maria@exemplo.com") =>
        ResidentBuilders.Resident(CondominiumId, name, email: email);
}
