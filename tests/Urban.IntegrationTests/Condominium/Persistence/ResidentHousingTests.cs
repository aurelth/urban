using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>Em que unidades uma conta mora hoje, lido do PostgreSQL de verdade: só vínculo de proprietário ou inquilino em vigor, sempre no condomínio da requisição.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class ResidentHousingTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly Today = new(2026, 11, 13);

    // ---- Quem mora onde ----

    [Fact]
    public async Task The_owner_lives_in_the_unit_with_block_and_number()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco B", "204");
        Guid account = await LinkedAsync(condominium, unit, OccupancyKind.Owner);

        ResidentDwelling dwelling = await GetAsync(condominium.Id, account);

        dwelling.Units.Should().Equal(new ResidentUnit(unit.Id, "Bloco B", "204"));
        dwelling.LivesAsDependent.Should().BeFalse();
    }

    [Fact]
    public async Task The_tenant_lives_in_the_unit_too()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco A", "101");
        Guid account = await LinkedAsync(condominium, unit, OccupancyKind.Tenant);

        (await GetAsync(condominium.Id, account)).Units.Select(found => found.UnitId).Should().Equal(unit.Id);
    }

    [Fact]
    public async Task Someone_with_two_units_gets_both_in_order_of_block_and_number()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit last = await SeedUnitAsync(condominium, "Bloco B", "101");
        CondominiumUnit second = await SeedUnitAsync(condominium, "Bloco A", "102");
        CondominiumUnit first = await SeedUnitAsync(condominium, "Bloco A", "101");
        Person person = ResidentBuilders.Resident(condominium.Id, "Dono de Tres");
        Guid account = Guid.CreateVersion7();
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, last.Id, OccupancyKind.Owner));
        await AddOccupancyAsync(condominium, person, second, OccupancyKind.Tenant);
        await AddOccupancyAsync(condominium, person, first, OccupancyKind.Owner);
        await LinkAsync(person.Id, account);

        ResidentDwelling dwelling = await GetAsync(condominium.Id, account);

        dwelling.Units.Select(found => (found.Block, found.Number)).Should().Equal(("Bloco A", "101"), ("Bloco A", "102"), ("Bloco B", "101"));
    }

    [Fact]
    public async Task Only_the_units_of_that_account_come_back_not_those_of_the_neighbours()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit mine = await SeedUnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit theirs = await SeedUnitAsync(condominium, "Bloco A", "102");
        Guid account = await LinkedAsync(condominium, mine, OccupancyKind.Owner);
        await LinkedAsync(condominium, theirs, OccupancyKind.Owner);

        (await GetAsync(condominium.Id, account)).Units.Select(found => found.UnitId).Should().Equal(mine.Id);
    }

    // ---- Vínculo em vigor ----

    [Fact]
    public async Task A_link_that_already_ended_no_longer_counts_but_one_ending_today_still_does()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit gone = await SeedUnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit leaving = await SeedUnitAsync(condominium, "Bloco A", "102");
        Person person = ResidentBuilders.Resident(condominium.Id, "Mudou de Casa");
        Guid account = Guid.CreateVersion7();
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, gone.Id, OccupancyKind.Tenant));
        await AddOccupancyAsync(condominium, person, leaving, OccupancyKind.Tenant);
        await LinkAsync(person.Id, account);
        await SetDatesAsync(person.Id, gone.Id, end: Today.AddDays(-1));
        await SetDatesAsync(person.Id, leaving.Id, end: Today);

        (await GetAsync(condominium.Id, account)).Units.Select(found => found.UnitId).Should().Equal(leaving.Id);
    }

    [Fact]
    public async Task A_link_that_has_not_started_yet_does_not_count()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco A", "101");
        (Guid account, Guid personId) = await LinkedPersonAsync(condominium, unit, OccupancyKind.Tenant);
        await SetDatesAsync(personId, unit.Id, start: Today.AddDays(5));

        ResidentDwelling dwelling = await GetAsync(condominium.Id, account);

        dwelling.Units.Should().BeEmpty();
        dwelling.LivesAsDependent.Should().BeFalse();
    }

    // ---- Dependente ----

    [Fact]
    public async Task A_dependent_has_no_unit_to_pay_but_is_known_to_live_as_a_dependent()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco A", "101");
        Guid account = await LinkedAsync(condominium, unit, OccupancyKind.Dependent);

        ResidentDwelling dwelling = await GetAsync(condominium.Id, account);

        dwelling.Units.Should().BeEmpty();
        dwelling.LivesAsDependent.Should().BeTrue();
    }

    [Fact]
    public async Task A_dependent_who_is_also_a_tenant_elsewhere_gets_the_unit_and_the_flag()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit family = await SeedUnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit own = await SeedUnitAsync(condominium, "Bloco A", "102");
        Person person = ResidentBuilders.Resident(condominium.Id, "Filha Independente");
        Guid account = Guid.CreateVersion7();
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, family.Id, OccupancyKind.Dependent));
        await AddOccupancyAsync(condominium, person, own, OccupancyKind.Tenant);
        await LinkAsync(person.Id, account);

        ResidentDwelling dwelling = await GetAsync(condominium.Id, account);

        dwelling.Units.Select(found => found.UnitId).Should().Equal(own.Id);
        dwelling.LivesAsDependent.Should().BeTrue();
    }

    [Fact]
    public async Task A_dependent_whose_link_ended_is_not_a_dependent_anymore()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco A", "101");
        (Guid account, Guid personId) = await LinkedPersonAsync(condominium, unit, OccupancyKind.Dependent);
        await SetDatesAsync(personId, unit.Id, end: Today.AddDays(-1));

        (await GetAsync(condominium.Id, account)).LivesAsDependent.Should().BeFalse();
    }

    // ---- Quem não tem vínculo e o isolamento ----

    [Fact]
    public async Task An_account_without_a_person_or_without_units_lives_nowhere()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await SeedUnitAsync(condominium, "Bloco A", "101");

        ResidentDwelling dwelling = await GetAsync(condominium.Id, Guid.CreateVersion7());

        dwelling.Units.Should().BeEmpty();
        dwelling.LivesAsDependent.Should().BeFalse();
    }

    [Fact]
    public async Task The_manager_who_is_not_a_resident_lives_nowhere()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await SeedUnitAsync(condominium, "Bloco A", "101");

        ResidentDwelling dwelling = await GetAsync(condominium.Id, condominium.CreatedBy);

        dwelling.Units.Should().BeEmpty();
        dwelling.LivesAsDependent.Should().BeFalse();
    }

    [Fact]
    public async Task Another_condominium_never_shows_the_units_of_the_account()
    {
        CondominiumEntity ours = await SeedCondominiumAsync();
        CondominiumEntity theirs = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(ours, "Bloco A", "101");
        Guid account = await LinkedAsync(ours, unit, OccupancyKind.Owner);

        ResidentDwelling dwelling = await GetAsync(theirs.Id, account);

        dwelling.Units.Should().BeEmpty();
        dwelling.LivesAsDependent.Should().BeFalse();
    }

    [Fact]
    public async Task Without_a_tenant_nothing_comes_back()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium, "Bloco A", "101");
        Guid account = await LinkedAsync(condominium, unit, OccupancyKind.Owner);

        (await GetAsync(null, account)).Units.Should().BeEmpty();
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

    private async Task<CondominiumUnit> SeedUnitAsync(CondominiumEntity condominium, string block, string number)
    {
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, block, number);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, ["number"], UnitBuilders.Now, unit.Id);
        (await new CondominiumUnits(db).AddAsync(unit, entry, Ct)).IsSuccess.Should().BeTrue();

        return unit;
    }

    /// <summary>Cadastra o morador na unidade com o vínculo pedido e liga a ele uma conta de acesso, como o convite aceito faz.</summary>
    private async Task<Guid> LinkedAsync(CondominiumEntity condominium, CondominiumUnit unit, OccupancyKind kind) =>
        (await LinkedPersonAsync(condominium, unit, kind)).Account;

    private async Task<(Guid Account, Guid PersonId)> LinkedPersonAsync(CondominiumEntity condominium, CondominiumUnit unit, OccupancyKind kind)
    {
        Person person = ResidentBuilders.Resident(condominium.Id);
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id, kind));
        Guid account = Guid.CreateVersion7();
        await LinkAsync(person.Id, account);

        return (account, person.Id);
    }

    private async Task AddAsync(CondominiumEntity condominium, Person person, Occupancy occupancy)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Resident, ["fullName", "cpf", "occupancy"], ResidentBuilders.Now, person.Id);
        (await new CondominiumResidents(db).AddAsync(person, occupancy, entry, Ct)).IsSuccess.Should().BeTrue();
    }

    /// <summary>Mais um vínculo da mesma pessoa, inserido direto no banco (o cadastro pela API só cria o primeiro).</summary>
    private async Task AddOccupancyAsync(CondominiumEntity condominium, Person person, CondominiumUnit unit, OccupancyKind kind)
    {
        Occupancy occupancy = ResidentBuilders.Occupancy(person, unit.Id, kind);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        db.Occupancies.Add(occupancy);
        await db.SaveChangesAsync(Ct);
    }

    private async Task LinkAsync(Guid personId, Guid userId)
    {
        await using CondominiumDbContext db = postgres.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"update condominium.person set user_id = {userId} where id = {personId}", Ct);
    }

    private async Task SetDatesAsync(Guid personId, Guid unitId, DateOnly? start = null, DateOnly? end = null)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        if (start is { } startDate)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"update condominium.occupancy set start_date = {startDate} where person_id = {personId} and unit_id = {unitId}", Ct);
        }

        if (end is { } endDate)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"update condominium.occupancy set end_date = {endDate} where person_id = {personId} and unit_id = {unitId}", Ct);
        }
    }

    private async Task<ResidentDwelling> GetAsync(Guid? tenant, Guid userId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new ResidentHousing(db, new FixedTenant(tenant)).GetAsync(userId, Today, Ct);
    }
}
