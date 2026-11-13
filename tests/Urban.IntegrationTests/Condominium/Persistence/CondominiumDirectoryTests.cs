using FluentAssertions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumDirectoryTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Names_come_back_by_id_whatever_the_tenant_of_the_request_and_unknown_ids_are_left_out()
    {
        CondominiumEntity sun = await SaveAsync("Edifício Sol");
        CondominiumEntity green = await SaveAsync("Residencial Vila Verde");
        await SaveAsync("Parque das Flores");

        IReadOnlyDictionary<Guid, string> names = await NamesAsync([sun.Id, green.Id, Guid.CreateVersion7()], tenant: Guid.CreateVersion7());

        names.Should().BeEquivalentTo(new Dictionary<Guid, string> { [sun.Id] = "Edifício Sol", [green.Id] = "Residencial Vila Verde" });
    }

    [Fact]
    public async Task Asking_for_no_ids_returns_nothing()
    {
        (await NamesAsync([])).Should().BeEmpty();
    }

    private async Task<CondominiumEntity> SaveAsync(string name)
    {
        CondominiumEntity condominium = CondominiumEntity.Register(name, CondominiumBuilders.UniqueCnpj(), CondominiumBuilders.SampleAddress(), Guid.CreateVersion7(), CondominiumBuilders.Now).Value;
        await using CondominiumDbContext db = postgres.CreateContext();
        (await new CondominiumRegistrations(db).SaveAsync(condominium, CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy), Ct)).IsSuccess.Should().BeTrue();

        return condominium;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, Guid? tenant = null)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);
        return await new CondominiumDirectory(db).GetNamesAsync(ids, Ct);
    }
}
