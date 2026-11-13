using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>Os moradores no PostgreSQL de verdade: esquema, vínculos, CPF único, busca, filtro por unidade, página, isolamento e registro de alterações.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumResidentsTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---- Esquema e migration ----

    [Fact]
    public async Task The_migration_creates_the_occupancy_table_with_the_partial_unique_index_of_active_occupancies()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        List<string> tables = await db.Database
            .SqlQuery<string>($"""select table_name as "Value" from information_schema.tables where table_schema = 'condominium'""")
            .ToListAsync(Ct);
        string index = await db.Database
            .SqlQuery<string>($"""select indexdef as "Value" from pg_indexes where schemaname = 'condominium' and indexname = 'ix_occupancy_person_unit_active'""")
            .SingleAsync(Ct);

        tables.Should().Contain("occupancy");
        index.Should().Contain("UNIQUE").And.Contain("person_id").And.Contain("unit_id").And.Contain("end_date IS NULL");
    }

    [Theory]
    [InlineData("user_id")]
    [InlineData("address_street")]
    [InlineData("address_number")]
    [InlineData("address_neighborhood")]
    [InlineData("address_city")]
    [InlineData("address_state")]
    [InlineData("address_postal_code")]
    [InlineData("email")]
    [InlineData("profession")]
    [InlineData("emergency_contact_name")]
    [InlineData("emergency_contact_relationship")]
    [InlineData("emergency_contact_phone")]
    public async Task The_person_columns_of_the_manager_only_data_accept_null_for_a_resident(string column)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        string nullable = await db.Database
            .SqlQuery<string>($"""
                select is_nullable as "Value" from information_schema.columns
                where table_schema = 'condominium' and table_name = 'person' and column_name = {column}
                """)
            .SingleAsync(Ct);

        nullable.Should().Be("YES");
    }

    [Theory]
    [InlineData("email", 254)]
    [InlineData("profession", 80)]
    [InlineData("emergency_contact_name", 120)]
    [InlineData("emergency_contact_relationship", 40)]
    [InlineData("emergency_contact_phone", 11)]
    public async Task The_new_person_columns_have_the_length_of_the_domain_rules(string column, int length)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        int? actual = await db.Database
            .SqlQuery<int?>($"""
                select character_maximum_length as "Value" from information_schema.columns
                where table_schema = 'condominium' and table_name = 'person' and column_name = {column}
                """)
            .SingleAsync(Ct);

        actual.Should().Be(length);
    }

    // ---- Cadastro e leitura ----

    [Fact]
    public async Task A_registered_resident_is_read_back_with_every_field_the_occupancy_and_the_unit_data()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco B", "202");
        Person person = ResidentBuilders.Resident(condominium.Id, "Maria Souza", email: "maria@exemplo.com");
        Occupancy occupancy = ResidentBuilders.Occupancy(person, unit.Id, OccupancyKind.Tenant, new DateOnly(2025, 2, 1));

        Result<string> added = await AddAsync(condominium, person, occupancy);
        ResidentRecord? record = await FindAsync(condominium, person.Id);

        added.IsSuccess.Should().BeTrue();
        added.Value.Should().NotBeNullOrWhiteSpace();
        record.Should().NotBeNull();
        record!.Version.Should().Be(added.Value);
        Person read = record.Person;
        read.UserId.Should().BeNull();
        read.Address.Should().BeNull();
        read.FullName.Value.Should().Be("Maria Souza");
        read.Cpf.Should().Be(person.Cpf);
        read.BirthDate.Value.Should().Be(new DateOnly(1990, 5, 20));
        read.Phone.Should().Be(person.Phone);
        read.Email!.Value.Should().Be("maria@exemplo.com");
        read.Profession!.Value.Should().Be("Engenheira");
        read.EmergencyContact!.Name.Value.Should().Be("Carlos Souza");
        read.EmergencyContact.Relationship.Should().Be("Irmão");
        read.EmergencyContact.Phone.Value.Should().Be("4733334444");
        ResidentOccupancy item = record.Occupancies.Should().ContainSingle().Subject;
        item.Occupancy.UnitId.Should().Be(unit.Id);
        item.Occupancy.Kind.Should().Be(OccupancyKind.Tenant);
        item.Occupancy.StartDate.Should().Be(new DateOnly(2025, 2, 1));
        item.Occupancy.EndDate.Should().BeNull();
        item.UnitBlock.Should().Be("Bloco B");
        item.UnitNumber.Should().Be("202");
    }

    [Fact]
    public async Task A_resident_without_email_is_read_back_without_email()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person person = ResidentBuilders.Resident(condominium.Id, email: null);
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id));

        ResidentRecord? record = await FindAsync(condominium, person.Id);

        record!.Person.Email.Should().BeNull();
    }

    [Fact]
    public async Task The_manager_of_the_condominium_still_reads_the_address_and_the_user_and_is_not_a_resident()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Person manager = await db.People.SingleAsync(Ct);
        ResidentRecord? asResident = await new CondominiumResidents(db).FindAsync(manager.Id, Ct);

        manager.UserId.Should().Be(condominium.CreatedBy);
        manager.RequireAddress().City.Should().Be("Blumenau");
        manager.Email.Should().BeNull();
        manager.EmergencyContact.Should().BeNull();
        asResident.Should().BeNull();
    }

    [Fact]
    public async Task The_cpf_is_stored_encrypted_and_never_in_the_clear_in_the_person_row_or_the_change_log()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person person = ResidentBuilders.Resident(condominium.Id);
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id));

        await using CondominiumDbContext db = postgres.CreateContext();
        string row = await db.Database.SqlQuery<string>($"""select to_jsonb(p)::text as "Value" from condominium.person p where id = {person.Id}""").SingleAsync(Ct);
        string log = await db.Database.SqlQuery<string>($"""select to_jsonb(c)::text as "Value" from condominium.change_log c where subject_id = {person.Id}""").SingleAsync(Ct);

        row.Should().NotContain(person.Cpf.Value);
        log.Should().NotContain(person.Cpf.Value);
    }

    // ---- CPF único ----

    [Fact]
    public async Task The_same_cpf_cannot_be_registered_twice_in_a_condominium_and_nothing_is_left_behind()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Urban.Condominium.Domain.Documents.Cpf cpf = CondominiumBuilders.UniqueCpf();
        Person first = ResidentBuilders.Resident(condominium.Id, "Maria Souza", cpf);
        Person second = ResidentBuilders.Resident(condominium.Id, "Outra Pessoa", cpf);
        await AddAsync(condominium, first, ResidentBuilders.Occupancy(first, unit.Id));

        Result<string> result = await AddAsync(condominium, second, ResidentBuilders.Occupancy(second, unit.Id));

        result.Error.Should().Be(PersonErrors.CpfAlreadyRegistered);
        await using CondominiumDbContext reader = postgres.CreateContext(condominium.Id);
        (await reader.Occupancies.CountAsync(Ct)).Should().Be(1);
        (await reader.People.CountAsync(p => p.Id == second.Id, Ct)).Should().Be(0);
        (await ChangeLogCountAsync(condominium.Id, "resident")).Should().Be(1);
    }

    [Fact]
    public async Task The_cpf_of_the_manager_also_counts_as_taken()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Urban.Condominium.Domain.Documents.Cpf managerCpf = (await db.People.SingleAsync(Ct)).Cpf;
        Person person = ResidentBuilders.Resident(condominium.Id, cpf: managerCpf);

        Result<string> result = await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id));

        result.Error.Should().Be(PersonErrors.CpfAlreadyRegistered);
    }

    [Fact]
    public async Task The_same_cpf_can_be_registered_in_two_different_condominiums()
    {
        CondominiumEntity first = await SeedCondominiumAsync();
        CondominiumEntity second = await SeedCondominiumAsync();
        CondominiumUnit firstUnit = await SeedUnitAsync(first);
        CondominiumUnit secondUnit = await SeedUnitAsync(second);
        Urban.Condominium.Domain.Documents.Cpf cpf = CondominiumBuilders.UniqueCpf();
        Person inFirst = ResidentBuilders.Resident(first.Id, cpf: cpf);
        Person inSecond = ResidentBuilders.Resident(second.Id, cpf: cpf);

        (await AddAsync(first, inFirst, ResidentBuilders.Occupancy(inFirst, firstUnit.Id))).IsSuccess.Should().BeTrue();
        (await AddAsync(second, inSecond, ResidentBuilders.Occupancy(inSecond, secondUnit.Id))).IsSuccess.Should().BeTrue();
    }

    // ---- Isolamento ----

    [Fact]
    public async Task A_resident_of_another_condominium_is_invisible_in_find_and_in_the_list()
    {
        CondominiumEntity mine = await SeedCondominiumAsync();
        CondominiumEntity other = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(other);
        Person stranger = ResidentBuilders.Resident(other.Id, "Pessoa De Fora");
        await AddAsync(other, stranger, ResidentBuilders.Occupancy(stranger, unit.Id));

        (await FindAsync(mine, stranger.Id)).Should().BeNull();
        (await ListAsync(mine, new ResidentFilter(null, null), 1, 50)).Total.Should().Be(0);
        (await ListAsync(other, new ResidentFilter(null, null), 1, 50)).Total.Should().Be(1);
    }

    [Fact]
    public async Task Without_a_condominium_in_the_request_nothing_is_found()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person person = ResidentBuilders.Resident(condominium.Id);
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id));

        await using CondominiumDbContext db = postgres.CreateContext(null);
        CondominiumResidents residents = new(db);

        (await residents.FindAsync(person.Id, Ct)).Should().BeNull();
        (await residents.ListAsync(new ResidentFilter(null, null), 1, 50, Ct)).Total.Should().Be(0);
    }

    // ---- Lista ----

    [Fact]
    public async Task The_list_is_ordered_by_name_and_paged_with_the_total_of_the_filter()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        foreach (string name in new[] { "Carla Dias", "Ana Lima", "Eduardo Prado", "Beto Rocha", "Daniela Melo" })
        {
            Person person = ResidentBuilders.Resident(condominium.Id, name);
            await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id));
        }

        PagedResidents first = await ListAsync(condominium, new ResidentFilter(null, null), 1, 2);
        PagedResidents second = await ListAsync(condominium, new ResidentFilter(null, null), 2, 2);
        PagedResidents last = await ListAsync(condominium, new ResidentFilter(null, null), 3, 2);
        PagedResidents beyond = await ListAsync(condominium, new ResidentFilter(null, null), 4, 2);

        first.Total.Should().Be(5);
        first.Items.Select(i => i.Person.FullName.Value).Should().Equal("Ana Lima", "Beto Rocha");
        second.Items.Select(i => i.Person.FullName.Value).Should().Equal("Carla Dias", "Daniela Melo");
        last.Items.Select(i => i.Person.FullName.Value).Should().Equal("Eduardo Prado");
        beyond.Items.Should().BeEmpty();
        beyond.Total.Should().Be(5);
        first.Items.Should().OnlyContain(i => i.Version.Length > 0 && i.Occupancies.Count == 1);
    }

    [Fact]
    public async Task The_list_does_not_include_the_manager()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        (await ListAsync(condominium, new ResidentFilter(null, null), 1, 50)).Total.Should().Be(0);
    }

    [Fact]
    public async Task The_search_matches_part_of_the_name_ignoring_case()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        await AddResidentAsync(condominium, unit, "Maria Souza");
        await AddResidentAsync(condominium, unit, "Maria Teresa");
        await AddResidentAsync(condominium, unit, "João Pereira");

        PagedResidents byPart = await ListAsync(condominium, new ResidentFilter("MARIA", null), 1, 50);
        PagedResidents byMiddle = await ListAsync(condominium, new ResidentFilter("pereira", null), 1, 50);
        PagedResidents none = await ListAsync(condominium, new ResidentFilter("zzz", null), 1, 50);

        byPart.Items.Select(i => i.Person.FullName.Value).Should().Equal("Maria Souza", "Maria Teresa");
        byPart.Total.Should().Be(2);
        byMiddle.Items.Should().ContainSingle().Which.Person.FullName.Value.Should().Be("João Pereira");
        none.Total.Should().Be(0);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    [InlineData("a%a")]
    [InlineData("Maria'; drop table condominium.person; --")]
    public async Task The_search_treats_wildcards_and_quotes_as_plain_text(string search)
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        await AddResidentAsync(condominium, unit, "Maria Souza");

        PagedResidents result = await ListAsync(condominium, new ResidentFilter(search, null), 1, 50);

        result.Total.Should().Be(0);
        (await ListAsync(condominium, new ResidentFilter(null, null), 1, 50)).Total.Should().Be(1);
    }

    [Fact]
    public async Task The_unit_filter_returns_only_who_has_an_occupancy_with_that_unit_and_keeps_all_their_occupancies()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit a = await SeedUnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit b = await SeedUnitAsync(condominium, "Bloco B", "202");
        Person inA = await AddResidentAsync(condominium, a, "Ana Lima");
        Person inB = await AddResidentAsync(condominium, b, "Beto Rocha");
        await AddOccupancyDirectlyAsync(condominium, ResidentBuilders.Occupancy(inA, b.Id, OccupancyKind.Tenant, new DateOnly(2025, 6, 1)));

        PagedResidents onlyA = await ListAsync(condominium, new ResidentFilter(null, a.Id), 1, 50);
        PagedResidents onlyB = await ListAsync(condominium, new ResidentFilter(null, b.Id), 1, 50);

        onlyA.Items.Select(i => i.Person.FullName.Value).Should().Equal("Ana Lima");
        onlyA.Items.Single().Occupancies.Should().HaveCount(2);
        onlyB.Items.Select(i => i.Person.FullName.Value).Should().Equal("Ana Lima", "Beto Rocha");
        onlyB.Total.Should().Be(2);
        inB.Id.Should().NotBe(inA.Id);
    }

    [Fact]
    public async Task A_resident_with_two_occupancies_appears_once_with_the_active_first_and_then_the_most_recent()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit old = await SeedUnitAsync(condominium, null, "10");
        CondominiumUnit recent = await SeedUnitAsync(condominium, null, "20");
        CondominiumUnit current = await SeedUnitAsync(condominium, null, "30");
        Person person = await AddResidentAsync(condominium, old, "Ana Lima", new DateOnly(2018, 1, 1));
        await AddOccupancyDirectlyAsync(condominium, ResidentBuilders.Occupancy(person, recent.Id, OccupancyKind.Tenant, new DateOnly(2021, 1, 1)));
        await AddOccupancyDirectlyAsync(condominium, ResidentBuilders.Occupancy(person, current.Id, OccupancyKind.Owner, new DateOnly(2019, 1, 1)));
        await EndOccupancyAsync(person.Id, old.Id, new DateOnly(2020, 12, 31));
        await EndOccupancyAsync(person.Id, recent.Id, new DateOnly(2022, 12, 31));

        PagedResidents list = await ListAsync(condominium, new ResidentFilter(null, null), 1, 50);
        ResidentRecord? found = await FindAsync(condominium, person.Id);

        list.Total.Should().Be(1);
        list.Items.Single().Occupancies.Select(o => o.UnitNumber).Should().Equal("30", "20", "10");
        found!.Occupancies.Select(o => o.UnitNumber).Should().Equal("30", "20", "10");
        found.Occupancies.Select(o => o.Occupancy.IsActive).Should().Equal(true, false, false);
    }

    // ---- Vínculos ----

    [Fact]
    public async Task A_person_cannot_have_two_active_occupancies_with_the_same_unit_but_can_return_after_leaving()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person person = await AddResidentAsync(condominium, unit, "Ana Lima");

        Func<Task> duplicate = () => AddOccupancyDirectlyAsync(condominium, ResidentBuilders.Occupancy(person, unit.Id, OccupancyKind.Tenant));
        DbUpdateException violation = (await duplicate.Should().ThrowAsync<DbUpdateException>()).Which;
        violation.InnerException.Should().BeOfType<PostgresException>().Which.ConstraintName.Should().Be("ix_occupancy_person_unit_active");

        await EndOccupancyAsync(person.Id, unit.Id, new DateOnly(2025, 12, 31));
        await AddOccupancyDirectlyAsync(condominium, ResidentBuilders.Occupancy(person, unit.Id, OccupancyKind.Tenant, new DateOnly(2026, 3, 1)));

        (await FindAsync(condominium, person.Id))!.Occupancies.Should().HaveCount(2);
    }

    [Fact]
    public async Task Two_different_people_can_live_in_the_same_unit()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);

        await AddResidentAsync(condominium, unit, "Ana Lima");
        await AddResidentAsync(condominium, unit, "Beto Rocha");

        (await ListAsync(condominium, new ResidentFilter(null, unit.Id), 1, 50)).Total.Should().Be(2);
    }

    // ---- Registro de alterações ----

    [Fact]
    public async Task The_change_log_is_written_in_the_same_save_with_the_resident_kind_the_subject_and_only_field_names()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person person = ResidentBuilders.Resident(condominium.Id);
        Guid actor = Guid.CreateVersion7();
        await using (CondominiumDbContext db = postgres.CreateContext(condominium.Id))
        {
            ChangeLogEntry entry = new(condominium.Id, actor, ChangeKind.Resident, ["fullName", "cpf", "occupancy"], ResidentBuilders.Now, person.Id);
            await new CondominiumResidents(db).AddAsync(person, ResidentBuilders.Occupancy(person, unit.Id), entry, Ct);
        }

        await using CondominiumDbContext reader = postgres.CreateContext();
        (await reader.Database.SqlQuery<string>($"""select kind as "Value" from condominium.change_log where subject_id = {person.Id}""").SingleAsync(Ct)).Should().Be("resident");
        (await reader.Database.SqlQuery<string>($"""select array_to_string(fields, ',') as "Value" from condominium.change_log where subject_id = {person.Id}""").SingleAsync(Ct)).Should().Be("fullName,cpf,occupancy");
        (await reader.Database.SqlQuery<Guid>($"""select actor_user_id as "Value" from condominium.change_log where subject_id = {person.Id}""").SingleAsync(Ct)).Should().Be(actor);
        (await reader.Database.SqlQuery<Guid>($"""select condominium_id as "Value" from condominium.change_log where subject_id = {person.Id}""").SingleAsync(Ct)).Should().Be(condominium.Id);
    }

    // ---- Apoio ----

    private async Task<CondominiumEntity> SeedCondominiumAsync()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person manager = CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy);
        await using CondominiumDbContext db = postgres.CreateContext();
        (await new CondominiumRegistrations(db).SaveAsync(condominium, manager, Ct)).IsSuccess.Should().BeTrue();

        return condominium;
    }

    private async Task<CondominiumUnit> SeedUnitAsync(CondominiumEntity condominium, string? block = "Bloco A", string? number = null)
    {
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, block, number ?? Guid.NewGuid().ToString("N")[..8]);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, ["number"], UnitBuilders.Now, unit.Id);
        (await new CondominiumUnits(db).AddAsync(unit, entry, Ct)).IsSuccess.Should().BeTrue();

        return unit;
    }

    private async Task<Person> AddResidentAsync(CondominiumEntity condominium, CondominiumUnit unit, string name, DateOnly? startDate = null)
    {
        Person person = ResidentBuilders.Resident(condominium.Id, name);
        (await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id, startDate: startDate))).IsSuccess.Should().BeTrue();

        return person;
    }

    private async Task<Result<string>> AddAsync(CondominiumEntity condominium, Person person, Occupancy occupancy)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Resident, ["fullName", "cpf", "occupancy"], ResidentBuilders.Now, person.Id);

        return await new CondominiumResidents(db).AddAsync(person, occupancy, entry, Ct);
    }

    private async Task<ResidentRecord?> FindAsync(CondominiumEntity condominium, Guid personId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumResidents(db).FindAsync(personId, Ct);
    }

    private async Task<PagedResidents> ListAsync(CondominiumEntity condominium, ResidentFilter filter, int page, int pageSize)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumResidents(db).ListAsync(filter, page, pageSize, Ct);
    }

    private async Task AddOccupancyDirectlyAsync(CondominiumEntity condominium, Occupancy occupancy)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        db.Occupancies.Add(occupancy);
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>Encerra o vínculo direto no banco: sair da unidade ainda não tem regra no Domain (vem na próxima fatia).</summary>
    private async Task EndOccupancyAsync(Guid personId, Guid unitId, DateOnly endDate)
    {
        await using CondominiumDbContext db = postgres.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"update condominium.occupancy set end_date = {endDate} where person_id = {personId} and unit_id = {unitId} and end_date is null",
            Ct);
    }

    private async Task<int> ChangeLogCountAsync(Guid condominiumId, string kind)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        return await db.Database
            .SqlQuery<int>($"""select count(*)::int as "Value" from condominium.change_log where condominium_id = {condominiumId} and kind = {kind}""")
            .SingleAsync(Ct);
    }
}
