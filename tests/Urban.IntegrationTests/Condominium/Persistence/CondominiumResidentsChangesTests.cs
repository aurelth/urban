using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>Editar o morador, abrir um vínculo novo e sair da unidade, no PostgreSQL de verdade: versão, conflitos, CPF único e registro de alterações.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumResidentsChangesTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---- Editar ----

    [Fact]
    public async Task Saving_an_edit_persists_every_field_and_returns_a_new_version()
    {
        (CondominiumEntity condominium, Person person, _) = await SeedResidentAsync();
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumResidents residents = new(db);
        ResidentRecord record = (await residents.FindAsync(person.Id, Ct))!;
        Cpf newCpf = CondominiumBuilders.UniqueCpf();

        record.Person.UpdateResident(
            PersonName.Create("Maria Souza Lima").Value,
            newCpf,
            BirthDate.Create(new DateOnly(1988, 1, 2), ResidentBuilders.Today).Value,
            PhoneNumber.Create("(47) 99111-2222").Value,
            ContactEmail.Create("nova@exemplo.com").Value,
            Profession.Create("Médica").Value,
            EmergencyContact.Create("Paulo Lima", "Esposo", "(47) 99333-4444").Value);
        Result<string> saved = await residents.SaveAsync(record, Entry(condominium, person.Id, "fullName", "cpf"), Ct);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(record.Version);
        ResidentRecord reread = (await FindAsync(condominium, person.Id))!;
        reread.Version.Should().Be(saved.Value);
        reread.Person.FullName.Value.Should().Be("Maria Souza Lima");
        reread.Person.Cpf.Should().Be(newCpf);
        reread.Person.BirthDate.Value.Should().Be(new DateOnly(1988, 1, 2));
        reread.Person.Phone.Value.Should().Be("47991112222");
        reread.Person.Email!.Value.Should().Be("nova@exemplo.com");
        reread.Person.Profession!.Value.Should().Be("Médica");
        reread.Person.EmergencyContact!.Name.Value.Should().Be("Paulo Lima");
        reread.Occupancies.Should().HaveCount(1);
    }

    [Fact]
    public async Task An_edit_can_clear_the_email()
    {
        (CondominiumEntity condominium, Person person, _) = await SeedResidentAsync();
        await using (CondominiumDbContext db = postgres.CreateContext(condominium.Id))
        {
            CondominiumResidents residents = new(db);
            ResidentRecord record = (await residents.FindAsync(person.Id, Ct))!;
            record.Person.UpdateResident(record.Person.FullName, record.Person.Cpf, record.Person.BirthDate, record.Person.Phone, null, record.Person.Profession!, record.Person.EmergencyContact!);
            (await residents.SaveAsync(record, Entry(condominium, person.Id, "email"), Ct)).IsSuccess.Should().BeTrue();
        }

        (await FindAsync(condominium, person.Id))!.Person.Email.Should().BeNull();
    }

    [Fact]
    public async Task A_stale_edit_is_a_conflict_and_the_first_one_stays()
    {
        (CondominiumEntity condominium, Person person, _) = await SeedResidentAsync();
        await using CondominiumDbContext first = postgres.CreateContext(condominium.Id);
        await using CondominiumDbContext second = postgres.CreateContext(condominium.Id);
        ResidentRecord a = (await new CondominiumResidents(first).FindAsync(person.Id, Ct))!;
        ResidentRecord b = (await new CondominiumResidents(second).FindAsync(person.Id, Ct))!;
        a.Person.UpdateResident(a.Person.FullName, a.Person.Cpf, a.Person.BirthDate, PhoneNumber.Create("(47) 98888-7777").Value, a.Person.Email, a.Person.Profession!, a.Person.EmergencyContact!);
        b.Person.UpdateResident(b.Person.FullName, b.Person.Cpf, b.Person.BirthDate, PhoneNumber.Create("(47) 97777-6666").Value, b.Person.Email, b.Person.Profession!, b.Person.EmergencyContact!);

        Result<string> saved = await new CondominiumResidents(first).SaveAsync(a, Entry(condominium, person.Id, "phone"), Ct);
        Result<string> stale = await new CondominiumResidents(second).SaveAsync(b, Entry(condominium, person.Id, "phone"), Ct);

        saved.IsSuccess.Should().BeTrue();
        stale.Error.Should().Be(Urban.Condominium.Domain.Condominiums.CondominiumErrors.ConcurrencyConflict);
        (await FindAsync(condominium, person.Id))!.Person.Phone.Value.Should().Be("47988887777");
        (await ChangeLogCountAsync(condominium.Id, "resident")).Should().Be(2); // o cadastro e a primeira edição
    }

    [Fact]
    public async Task Changing_the_cpf_to_one_already_taken_in_the_condominium_is_refused_and_nothing_changes()
    {
        (CondominiumEntity condominium, Person person, CondominiumUnit unit) = await SeedResidentAsync();
        Person other = ResidentBuilders.Resident(condominium.Id, "Outra Pessoa");
        await AddAsync(condominium, other, ResidentBuilders.Occupancy(other, unit.Id));
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumResidents residents = new(db);
        ResidentRecord record = (await residents.FindAsync(person.Id, Ct))!;
        record.Person.UpdateResident(record.Person.FullName, other.Cpf, record.Person.BirthDate, record.Person.Phone, record.Person.Email, record.Person.Profession!, record.Person.EmergencyContact!);

        Result<string> result = await residents.SaveAsync(record, Entry(condominium, person.Id, "cpf"), Ct);

        result.Error.Should().Be(PersonErrors.CpfAlreadyRegistered);
        (await FindAsync(condominium, person.Id))!.Person.Cpf.Should().Be(person.Cpf);
        (await ChangeLogCountAsync(condominium.Id, "resident")).Should().Be(2); // só os dois cadastros
    }

    // ---- Novo vínculo ----

    [Fact]
    public async Task Adding_an_occupancy_persists_it_with_the_log_and_the_resident_reads_both()
    {
        (CondominiumEntity condominium, Person person, _) = await SeedResidentAsync();
        CondominiumUnit other = await SeedUnitAsync(condominium, "Bloco B", "202");
        Occupancy added = ResidentBuilders.Occupancy(person, other.Id, OccupancyKind.Tenant, new DateOnly(2025, 6, 1));

        Result result = await AddOccupancyAsync(condominium, added);

        result.IsSuccess.Should().BeTrue();
        ResidentRecord reread = (await FindAsync(condominium, person.Id))!;
        reread.Occupancies.Should().HaveCount(2);
        reread.Occupancies.Should().ContainSingle(o => o.Occupancy.Id == added.Id).Which.UnitNumber.Should().Be("202");
        (await ChangeLogCountAsync(condominium.Id, "resident")).Should().Be(2);
    }

    [Fact]
    public async Task Adding_a_second_active_occupancy_in_the_same_unit_is_refused_and_leaves_no_log()
    {
        (CondominiumEntity condominium, Person person, CondominiumUnit unit) = await SeedResidentAsync();

        Result result = await AddOccupancyAsync(condominium, ResidentBuilders.Occupancy(person, unit.Id, OccupancyKind.Tenant));

        result.Error.Should().Be(OccupancyErrors.AlreadyActive);
        (await FindAsync(condominium, person.Id))!.Occupancies.Should().HaveCount(1);
        (await ChangeLogCountAsync(condominium.Id, "resident")).Should().Be(1);
    }

    // ---- Sair da unidade ----

    [Fact]
    public async Task Ending_an_occupancy_fills_the_end_date_keeps_the_history_and_logs_it()
    {
        (CondominiumEntity condominium, Person person, _) = await SeedResidentAsync();
        Occupancy occupancy = (await FindAsync(condominium, person.Id))!.Occupancies.Single().Occupancy;
        occupancy.MoveOut(new DateOnly(2026, 10, 31), ResidentBuilders.Today);

        Result result = await EndOccupancyAsync(condominium, occupancy);

        result.IsSuccess.Should().BeTrue();
        ResidentRecord reread = (await FindAsync(condominium, person.Id))!;
        reread.Occupancies.Single().Occupancy.EndDate.Should().Be(new DateOnly(2026, 10, 31));
        reread.Occupancies.Single().Occupancy.IsActive.Should().BeFalse();
        (await ChangeLogCountAsync(condominium.Id, "resident")).Should().Be(2);
    }

    [Fact]
    public async Task Ending_an_occupancy_twice_at_the_same_time_lets_only_the_first_through()
    {
        (CondominiumEntity condominium, Person person, _) = await SeedResidentAsync();
        Occupancy first = (await FindAsync(condominium, person.Id))!.Occupancies.Single().Occupancy;
        Occupancy second = (await FindAsync(condominium, person.Id))!.Occupancies.Single().Occupancy;
        first.MoveOut(new DateOnly(2026, 10, 31), ResidentBuilders.Today);
        second.MoveOut(new DateOnly(2026, 9, 30), ResidentBuilders.Today);

        Result one = await EndOccupancyAsync(condominium, first);
        Result two = await EndOccupancyAsync(condominium, second);

        one.IsSuccess.Should().BeTrue();
        two.Error.Should().Be(OccupancyErrors.AlreadyEnded);
        (await FindAsync(condominium, person.Id))!.Occupancies.Single().Occupancy.EndDate.Should().Be(new DateOnly(2026, 10, 31));
        (await ChangeLogCountAsync(condominium.Id, "resident")).Should().Be(2);
    }

    [Fact]
    public async Task After_leaving_every_unit_the_person_is_still_a_resident_in_the_list_and_in_the_unit_filter_with_the_history()
    {
        (CondominiumEntity condominium, Person person, CondominiumUnit unit) = await SeedResidentAsync();
        Occupancy occupancy = (await FindAsync(condominium, person.Id))!.Occupancies.Single().Occupancy;
        occupancy.MoveOut(new DateOnly(2026, 10, 31), ResidentBuilders.Today);
        await EndOccupancyAsync(condominium, occupancy);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        PagedResidents all = await new CondominiumResidents(db).ListAsync(new ResidentFilter(null, null), 1, 50, Ct);
        PagedResidents byUnit = await new CondominiumResidents(db).ListAsync(new ResidentFilter(null, unit.Id), 1, 50, Ct);

        all.Total.Should().Be(1);
        byUnit.Total.Should().Be(1);
        byUnit.Items.Single().Occupancies.Single().Occupancy.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task The_same_person_can_return_to_a_unit_after_leaving_it()
    {
        (CondominiumEntity condominium, Person person, CondominiumUnit unit) = await SeedResidentAsync();
        Occupancy old = (await FindAsync(condominium, person.Id))!.Occupancies.Single().Occupancy;
        old.MoveOut(new DateOnly(2026, 1, 31), ResidentBuilders.Today);
        await EndOccupancyAsync(condominium, old);

        Result result = await AddOccupancyAsync(condominium, ResidentBuilders.Occupancy(person, unit.Id, OccupancyKind.Tenant, new DateOnly(2026, 3, 1)));

        result.IsSuccess.Should().BeTrue();
        (await FindAsync(condominium, person.Id))!.Occupancies.Select(o => o.Occupancy.IsActive).Should().Equal(true, false);
    }

    // ---- Apoio ----

    private async Task<(CondominiumEntity Condominium, Person Person, CondominiumUnit Unit)> SeedResidentAsync()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person manager = CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy);
        await using (CondominiumDbContext db = postgres.CreateContext())
        {
            (await new CondominiumRegistrations(db).SaveAsync(condominium, manager, Ct)).IsSuccess.Should().BeTrue();
        }

        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco A", "101");
        Person person = ResidentBuilders.Resident(condominium.Id);
        (await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id))).IsSuccess.Should().BeTrue();

        return (condominium, person, unit);
    }

    private async Task<CondominiumUnit> SeedUnitAsync(CondominiumEntity condominium, string? block, string number)
    {
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, block, number);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, ["number"], UnitBuilders.Now, unit.Id);
        (await new CondominiumUnits(db).AddAsync(unit, entry, Ct)).IsSuccess.Should().BeTrue();

        return unit;
    }

    private static ChangeLogEntry Entry(CondominiumEntity condominium, Guid personId, params string[] fields) =>
        new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Resident, fields, ResidentBuilders.Now, personId);

    private async Task<Result<string>> AddAsync(CondominiumEntity condominium, Person person, Occupancy occupancy)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumResidents(db).AddAsync(person, occupancy, Entry(condominium, person.Id, "fullName"), Ct);
    }

    private async Task<Result> AddOccupancyAsync(CondominiumEntity condominium, Occupancy occupancy)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumResidents(db).AddOccupancyAsync(occupancy, Entry(condominium, occupancy.PersonId, "occupancy"), Ct);
    }

    private async Task<Result> EndOccupancyAsync(CondominiumEntity condominium, Occupancy occupancy)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumResidents(db).EndOccupancyAsync(occupancy, Entry(condominium, occupancy.PersonId, "occupancyEnd"), Ct);
    }

    private async Task<ResidentRecord?> FindAsync(CondominiumEntity condominium, Guid personId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumResidents(db).FindAsync(personId, Ct);
    }

    private async Task<int> ChangeLogCountAsync(Guid condominiumId, string kind)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        return await db.Database
            .SqlQuery<int>($"""select count(*)::int as "Value" from condominium.change_log where condominium_id = {condominiumId} and kind = {kind}""")
            .SingleAsync(Ct);
    }
}
