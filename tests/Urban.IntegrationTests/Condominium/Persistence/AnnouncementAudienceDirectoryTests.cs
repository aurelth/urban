using FluentAssertions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>A quem um comunicado chega, lido do PostgreSQL de verdade: blocos, unidades e as contas de quem mora hoje, sempre no condomínio da requisição.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class AnnouncementAudienceDirectoryTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly Today = new(2026, 11, 22);

    private readonly CondominiumSeedKit _seed = new(postgres);

    // ---- Blocos ----

    [Fact]
    public async Task A_block_exists_when_the_condominium_has_a_unit_in_it()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        await _seed.UnitAsync(condominium, "Bloco A", "101");

        (await BlockExistsAsync(condominium.Id, "Bloco A")).Should().BeTrue();
    }

    [Theory]
    [InlineData("Bloco B")]
    [InlineData("bloco a")]
    [InlineData("Bloco")]
    public async Task A_block_with_no_units_does_not_exist_and_the_name_is_compared_exactly(string block)
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        await _seed.UnitAsync(condominium, "Bloco A", "101");

        (await BlockExistsAsync(condominium.Id, block)).Should().BeFalse();
    }

    [Fact]
    public async Task The_blocks_of_another_condominium_do_not_count_and_without_a_tenant_there_are_none()
    {
        CondominiumEntity ours = await _seed.CondominiumAsync();
        CondominiumEntity theirs = await _seed.CondominiumAsync();
        await _seed.UnitAsync(theirs, "Bloco Z", "101");

        (await BlockExistsAsync(ours.Id, "Bloco Z")).Should().BeFalse();
        (await BlockExistsAsync(null, "Bloco Z")).Should().BeFalse();
    }

    // ---- Unidades ----

    [Fact]
    public async Task A_unit_comes_with_its_block_and_number()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit unit = await _seed.UnitAsync(condominium, "Bloco B", "204");

        ResidentUnit? found = await FindUnitAsync(condominium.Id, unit.Id);

        found.Should().Be(new ResidentUnit(unit.Id, "Bloco B", "204"));
    }

    [Fact]
    public async Task A_unit_of_another_condominium_or_unknown_is_not_found_and_without_a_tenant_nothing_is()
    {
        CondominiumEntity ours = await _seed.CondominiumAsync();
        CondominiumEntity theirs = await _seed.CondominiumAsync();
        CondominiumUnit theirUnit = await _seed.UnitAsync(theirs, "Bloco A", "101");
        CondominiumUnit ourUnit = await _seed.UnitAsync(ours, "Bloco A", "101");

        (await FindUnitAsync(ours.Id, theirUnit.Id)).Should().BeNull();
        (await FindUnitAsync(ours.Id, Guid.CreateVersion7())).Should().BeNull();
        (await FindUnitAsync(null, ourUnit.Id)).Should().BeNull();
    }

    // ---- Em que unidades a conta mora ----

    [Theory]
    [InlineData(OccupancyKind.Owner)]
    [InlineData(OccupancyKind.Tenant)]
    [InlineData(OccupancyKind.Dependent)]
    public async Task Whoever_lives_in_the_unit_lives_there_in_any_role_even_as_a_dependent(OccupancyKind kind)
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit unit = await _seed.UnitAsync(condominium, "Bloco B", "204");
        Guid account = await _seed.AccountAsync(condominium, unit, kind);

        (await GetUnitsAsync(condominium.Id, account)).Should().Equal(new ResidentUnit(unit.Id, "Bloco B", "204"));
    }

    [Fact]
    public async Task Someone_with_two_units_gets_both_in_order_of_block_and_number()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit last = await _seed.UnitAsync(condominium, "Bloco B", "101");
        CondominiumUnit second = await _seed.UnitAsync(condominium, "Bloco A", "102");
        CondominiumUnit first = await _seed.UnitAsync(condominium, "Bloco A", "101");
        Person person = await _seed.PersonAsync(condominium, last, OccupancyKind.Owner);
        await _seed.AddOccupancyAsync(condominium, person, second, OccupancyKind.Tenant);
        await _seed.AddOccupancyAsync(condominium, person, first, OccupancyKind.Dependent);
        Guid account = Guid.CreateVersion7();
        await _seed.LinkAsync(person.Id, account);

        IReadOnlyList<ResidentUnit> units = await GetUnitsAsync(condominium.Id, account);

        units.Select(unit => (unit.Block, unit.Number)).Should().Equal(("Bloco A", "101"), ("Bloco A", "102"), ("Bloco B", "101"));
    }

    [Fact]
    public async Task Only_the_units_of_that_account_come_back_not_those_of_the_neighbours()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit mine = await _seed.UnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit theirs = await _seed.UnitAsync(condominium, "Bloco A", "102");
        Guid account = await _seed.AccountAsync(condominium, mine, OccupancyKind.Owner);
        await _seed.AccountAsync(condominium, theirs, OccupancyKind.Owner);

        (await GetUnitsAsync(condominium.Id, account)).Select(unit => unit.UnitId).Should().Equal(mine.Id);
    }

    [Fact]
    public async Task A_link_that_ended_no_longer_counts_but_one_ending_today_still_does_and_one_not_started_does_not()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit gone = await _seed.UnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit leaving = await _seed.UnitAsync(condominium, "Bloco A", "102");
        CondominiumUnit future = await _seed.UnitAsync(condominium, "Bloco A", "103");
        Person person = await _seed.PersonAsync(condominium, gone, OccupancyKind.Tenant);
        await _seed.AddOccupancyAsync(condominium, person, leaving, OccupancyKind.Tenant);
        await _seed.AddOccupancyAsync(condominium, person, future, OccupancyKind.Tenant);
        Guid account = Guid.CreateVersion7();
        await _seed.LinkAsync(person.Id, account);
        await _seed.SetDatesAsync(person.Id, gone.Id, end: Today.AddDays(-1));
        await _seed.SetDatesAsync(person.Id, leaving.Id, end: Today);
        await _seed.SetDatesAsync(person.Id, future.Id, start: Today.AddDays(5));

        (await GetUnitsAsync(condominium.Id, account)).Select(unit => unit.UnitId).Should().Equal(leaving.Id);
    }

    [Fact]
    public async Task The_manager_who_lives_nowhere_an_unknown_account_and_another_condominium_get_no_units()
    {
        CondominiumEntity ours = await _seed.CondominiumAsync();
        CondominiumEntity theirs = await _seed.CondominiumAsync();
        CondominiumUnit unit = await _seed.UnitAsync(ours, "Bloco A", "101");
        Guid account = await _seed.AccountAsync(ours, unit, OccupancyKind.Owner);

        (await GetUnitsAsync(ours.Id, ours.CreatedBy)).Should().BeEmpty();
        (await GetUnitsAsync(ours.Id, Guid.CreateVersion7())).Should().BeEmpty();
        (await GetUnitsAsync(theirs.Id, account)).Should().BeEmpty();
        (await GetUnitsAsync(null, account)).Should().BeEmpty();
    }

    // ---- Quem recebe ----

    [Fact]
    public async Task The_whole_condominium_reaches_every_account_that_lives_there_in_any_role()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit a101 = await _seed.UnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit b201 = await _seed.UnitAsync(condominium, "Bloco B", "201");
        Guid owner = await _seed.AccountAsync(condominium, a101, OccupancyKind.Owner);
        Guid dependent = await _seed.AccountAsync(condominium, a101, OccupancyKind.Dependent);
        Guid tenant = await _seed.AccountAsync(condominium, b201, OccupancyKind.Tenant);

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await GetRecipientsAsync(condominium.Id, new AudienceTarget(null, null));

        recipients.Should().ContainSingle().Which.Should().BeEquivalentTo([owner, dependent, tenant]);
    }

    [Fact]
    public async Task A_block_reaches_only_the_accounts_of_that_block()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit a101 = await _seed.UnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit b201 = await _seed.UnitAsync(condominium, "Bloco B", "201");
        Guid inA = await _seed.AccountAsync(condominium, a101, OccupancyKind.Owner);
        await _seed.AccountAsync(condominium, b201, OccupancyKind.Owner);

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await GetRecipientsAsync(condominium.Id, new AudienceTarget("Bloco A", null));

        recipients.Single().Should().BeEquivalentTo([inA]);
    }

    [Fact]
    public async Task A_unit_reaches_only_the_accounts_of_that_unit()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit a101 = await _seed.UnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit a102 = await _seed.UnitAsync(condominium, "Bloco A", "102");
        Guid owner = await _seed.AccountAsync(condominium, a101, OccupancyKind.Owner);
        Guid dependent = await _seed.AccountAsync(condominium, a101, OccupancyKind.Dependent);
        await _seed.AccountAsync(condominium, a102, OccupancyKind.Owner);

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await GetRecipientsAsync(condominium.Id, new AudienceTarget(null, a101.Id));

        recipients.Single().Should().BeEquivalentTo([owner, dependent]);
    }

    [Fact]
    public async Task Several_targets_come_back_in_one_call_in_the_order_asked()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit a101 = await _seed.UnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit b201 = await _seed.UnitAsync(condominium, "Bloco B", "201");
        Guid inA = await _seed.AccountAsync(condominium, a101, OccupancyKind.Owner);
        Guid inB = await _seed.AccountAsync(condominium, b201, OccupancyKind.Owner);

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await GetRecipientsAsync(
            condominium.Id, new AudienceTarget("Bloco B", null), new AudienceTarget(null, null), new AudienceTarget(null, a101.Id), new AudienceTarget("Bloco Z", null));

        recipients.Should().HaveCount(4);
        recipients[0].Should().BeEquivalentTo([inB]);
        recipients[1].Should().BeEquivalentTo([inA, inB]);
        recipients[2].Should().BeEquivalentTo([inA]);
        recipients[3].Should().BeEmpty();
    }

    [Fact]
    public async Task Someone_living_in_two_units_of_the_scope_counts_once()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit a101 = await _seed.UnitAsync(condominium, "Bloco A", "101");
        CondominiumUnit a102 = await _seed.UnitAsync(condominium, "Bloco A", "102");
        Person person = await _seed.PersonAsync(condominium, a101, OccupancyKind.Owner);
        await _seed.AddOccupancyAsync(condominium, person, a102, OccupancyKind.Tenant);
        Guid account = Guid.CreateVersion7();
        await _seed.LinkAsync(person.Id, account);

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await GetRecipientsAsync(condominium.Id, new AudienceTarget("Bloco A", null));

        recipients.Single().Should().Equal([account]);
    }

    [Fact]
    public async Task People_without_an_account_and_links_that_ended_or_have_not_started_do_not_receive()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit a101 = await _seed.UnitAsync(condominium, "Bloco A", "101");
        await _seed.PersonAsync(condominium, a101, OccupancyKind.Owner);
        (_, Guid movedOut) = await _seed.LinkedAsync(condominium, a101, OccupancyKind.Tenant);
        (_, Guid arriving) = await _seed.LinkedAsync(condominium, a101, OccupancyKind.Tenant);
        (Guid leavingToday, Guid leavingPerson) = await _seed.LinkedAsync(condominium, a101, OccupancyKind.Tenant);
        await _seed.SetDatesAsync(movedOut, a101.Id, end: Today.AddDays(-1));
        await _seed.SetDatesAsync(arriving, a101.Id, start: Today.AddDays(3));
        await _seed.SetDatesAsync(leavingPerson, a101.Id, end: Today);

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await GetRecipientsAsync(condominium.Id, new AudienceTarget(null, null));

        recipients.Single().Should().BeEquivalentTo([leavingToday]);
    }

    [Fact]
    public async Task The_manager_who_does_not_live_there_is_not_a_recipient_and_the_one_who_does_is_listed_for_the_caller_to_drop()
    {
        CondominiumEntity condominium = await _seed.CondominiumAsync();
        CondominiumUnit a101 = await _seed.UnitAsync(condominium, "Bloco A", "101");
        Guid resident = await _seed.AccountAsync(condominium, a101, OccupancyKind.Owner);

        IReadOnlyList<IReadOnlySet<Guid>> recipients = await GetRecipientsAsync(condominium.Id, new AudienceTarget(null, null));

        recipients.Single().Should().BeEquivalentTo([resident]).And.NotContain(condominium.CreatedBy);
    }

    [Fact]
    public async Task Another_condominium_never_receives_and_without_a_tenant_or_targets_nobody_does()
    {
        CondominiumEntity ours = await _seed.CondominiumAsync();
        CondominiumEntity theirs = await _seed.CondominiumAsync();
        CondominiumUnit theirUnit = await _seed.UnitAsync(theirs, "Bloco A", "101");
        await _seed.AccountAsync(theirs, theirUnit, OccupancyKind.Owner);

        (await GetRecipientsAsync(ours.Id, new AudienceTarget(null, null))).Single().Should().BeEmpty();
        (await GetRecipientsAsync(null, new AudienceTarget(null, null))).Single().Should().BeEmpty();
        (await GetRecipientsAsync(theirs.Id)).Should().BeEmpty();
    }

    // ---- Apoio ----

    private async Task<bool> BlockExistsAsync(Guid? tenant, string block)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new AnnouncementAudienceDirectory(db, new FixedTenant(tenant)).BlockExistsAsync(block, Ct);
    }

    private async Task<ResidentUnit?> FindUnitAsync(Guid? tenant, Guid unitId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new AnnouncementAudienceDirectory(db, new FixedTenant(tenant)).FindUnitAsync(unitId, Ct);
    }

    private async Task<IReadOnlyList<ResidentUnit>> GetUnitsAsync(Guid? tenant, Guid userId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new AnnouncementAudienceDirectory(db, new FixedTenant(tenant)).GetUnitsAsync(userId, Today, Ct);
    }

    private async Task<IReadOnlyList<IReadOnlySet<Guid>>> GetRecipientsAsync(Guid? tenant, params AudienceTarget[] targets)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new AnnouncementAudienceDirectory(db, new FixedTenant(tenant)).GetRecipientsAsync(targets, Today, Ct);
    }
}
