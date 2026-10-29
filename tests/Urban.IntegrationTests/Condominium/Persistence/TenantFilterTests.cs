using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

[Collection(CondominiumPostgresCollection.Name)]
public sealed class TenantFilterTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_condominium_sees_only_its_own_people()
    {
        (CondominiumEntity a, Person personA) = await SeedAsync();
        (CondominiumEntity b, Person personB) = await SeedAsync();

        await using CondominiumDbContext asA = postgres.CreateContext(a.Id);
        await using CondominiumDbContext asB = postgres.CreateContext(b.Id);

        (await asA.People.Select(p => p.Id).ToListAsync(Ct)).Should().Contain(personA.Id).And.NotContain(personB.Id);
        (await asB.People.Select(p => p.Id).ToListAsync(Ct)).Should().Contain(personB.Id).And.NotContain(personA.Id);
    }

    [Fact]
    public async Task Without_a_tenant_no_person_is_visible()
    {
        await SeedAsync();

        await using CondominiumDbContext db = postgres.CreateContext(tenant: null);

        (await db.People.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Asking_for_a_person_of_another_condominium_by_id_finds_nothing()
    {
        (_, Person victim) = await SeedAsync();
        (CondominiumEntity attacker, _) = await SeedAsync();

        await using CondominiumDbContext db = postgres.CreateContext(attacker.Id);

        (await db.People.SingleOrDefaultAsync(p => p.Id == victim.Id, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task Ignoring_the_filter_is_the_explicit_way_to_see_every_condominium()
    {
        (_, Person personA) = await SeedAsync();
        (_, Person personB) = await SeedAsync();

        await using CondominiumDbContext db = postgres.CreateContext(tenant: null);
        List<Guid> everyone = await db.People.IgnoreQueryFilters().Select(p => p.Id).ToListAsync(Ct);

        everyone.Should().Contain([personA.Id, personB.Id]);
    }

    [Fact]
    public async Task The_condominium_table_itself_is_outside_the_filter()
    {
        (CondominiumEntity a, _) = await SeedAsync();
        (CondominiumEntity b, _) = await SeedAsync();

        await using CondominiumDbContext db = postgres.CreateContext(tenant: null);
        List<Guid> ids = await db.Condominiums.Select(c => c.Id).ToListAsync(Ct);

        ids.Should().Contain([a.Id, b.Id]);
    }

    private async Task<(CondominiumEntity Condominium, Person Person)> SeedAsync()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person person = CondominiumBuilders.Manager(condominium.Id);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        db.Condominiums.Add(condominium);
        db.People.Add(person);
        await db.SaveChangesAsync(Ct);

        return (condominium, person);
    }
}
