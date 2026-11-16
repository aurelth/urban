using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Condominium.Application.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>Quem emite e quem recebe a fatura, lido do PostgreSQL de verdade: o condomínio, o responsável de cada unidade na data e o isolamento por tenant.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class InvoicePartiesTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly Today = new(2026, 11, 13);

    // ---- O condomínio ----

    [Fact]
    public async Task The_condominium_comes_with_the_name_the_formatted_cnpj_and_the_address_in_one_line()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        InvoiceCondominium? found = await GetCondominiumAsync(condominium.Id);

        found.Should().Be(new InvoiceCondominium(condominium.Name, condominium.Cnpj.Formatted, condominium.Address.Formatted));
    }

    [Fact]
    public async Task Without_a_tenant_there_is_no_condominium()
    {
        await SeedCondominiumAsync();

        (await GetCondominiumAsync(null)).Should().BeNull();
    }

    [Fact]
    public async Task A_condominium_never_shows_the_data_of_another_one()
    {
        CondominiumEntity ours = await SeedCondominiumAsync();
        CondominiumEntity theirs = await SeedCondominiumAsync();

        InvoiceCondominium? found = await GetCondominiumAsync(ours.Id);

        found!.Cnpj.Should().Be(ours.Cnpj.Formatted).And.NotBe(theirs.Cnpj.Formatted);
    }

    // ---- O responsável ----

    [Fact]
    public async Task The_tenant_who_lives_there_is_the_responsible_and_the_owner_is_the_fallback()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit rented = await SeedUnitAsync(condominium);
        CondominiumUnit owned = await SeedUnitAsync(condominium);
        await AddAsync(condominium, rented, "Paulo Dono", OccupancyKind.Owner);
        Person tenant = await AddAsync(condominium, rented, "Marina Inquilina", OccupancyKind.Tenant);
        Person owner = await AddAsync(condominium, owned, "Carlos Proprietario", OccupancyKind.Owner);

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [rented.Id, owned.Id]);

        responsibles.Should().HaveCount(2);
        responsibles[rented.Id].Name.Should().Be("Marina Inquilina");
        responsibles[rented.Id].Email.Should().Be(tenant.Email!.Value);
        responsibles[owned.Id].Name.Should().Be("Carlos Proprietario");
        responsibles[owned.Id].Email.Should().Be(owner.Email!.Value);
    }

    [Fact]
    public async Task A_tenant_who_already_left_and_a_dependent_never_receive_the_invoice()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        CondominiumUnit onlyDependent = await SeedUnitAsync(condominium);
        await AddAsync(condominium, unit, "Paulo Dono", OccupancyKind.Owner);
        Person leftTenant = await AddAsync(condominium, unit, "Antigo Inquilino", OccupancyKind.Tenant);
        await EndOccupancyAsync(leftTenant.Id, unit.Id, Today.AddDays(-1));
        await AddAsync(condominium, onlyDependent, "Filha Dependente", OccupancyKind.Dependent);

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [unit.Id, onlyDependent.Id]);

        responsibles.Keys.Should().Equal(unit.Id);
        responsibles[unit.Id].Name.Should().Be("Paulo Dono");
    }

    [Fact]
    public async Task A_responsible_without_an_email_still_comes_back_with_a_null_email()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person person = ResidentBuilders.Resident(condominium.Id, "Sem Email", email: null);
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id, OccupancyKind.Owner));

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [unit.Id]);

        responsibles[unit.Id].Should().Match<InvoiceResponsible>(found => found.Name == "Sem Email" && found.Email == null);
    }

    [Fact]
    public async Task A_unit_without_anyone_and_units_of_another_condominium_are_left_out()
    {
        CondominiumEntity ours = await SeedCondominiumAsync();
        CondominiumEntity theirs = await SeedCondominiumAsync();
        CondominiumUnit empty = await SeedUnitAsync(ours);
        CondominiumUnit foreign = await SeedUnitAsync(theirs);
        await AddAsync(theirs, foreign, "Vizinho de Outro Predio", OccupancyKind.Owner);

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(ours.Id, [empty.Id, foreign.Id]);

        responsibles.Should().BeEmpty();
    }

    [Fact]
    public async Task Asking_for_no_units_returns_nothing()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        (await GetResponsiblesAsync(condominium.Id, [])).Should().BeEmpty();
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

    private async Task<CondominiumUnit> SeedUnitAsync(CondominiumEntity condominium)
    {
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, "Bloco A", Guid.NewGuid().ToString("N")[..8]);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, ["number"], UnitBuilders.Now, unit.Id);
        (await new CondominiumUnits(db).AddAsync(unit, entry, Ct)).IsSuccess.Should().BeTrue();

        return unit;
    }

    private async Task<Person> AddAsync(CondominiumEntity condominium, CondominiumUnit unit, string name, OccupancyKind kind)
    {
        Person person = ResidentBuilders.Resident(condominium.Id, name);
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id, kind));

        return person;
    }

    private async Task AddAsync(CondominiumEntity condominium, Person person, Occupancy occupancy)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Resident, ["fullName", "cpf", "occupancy"], ResidentBuilders.Now, person.Id);
        (await new CondominiumResidents(db).AddAsync(person, occupancy, entry, Ct)).IsSuccess.Should().BeTrue();
    }

    /// <summary>Encerra o vínculo direto no banco, como os outros testes de persistência fazem.</summary>
    private async Task EndOccupancyAsync(Guid personId, Guid unitId, DateOnly endDate)
    {
        await using CondominiumDbContext db = postgres.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"update condominium.occupancy set end_date = {endDate} where person_id = {personId} and unit_id = {unitId} and end_date is null",
            Ct);
    }

    private async Task<InvoiceCondominium?> GetCondominiumAsync(Guid? tenant)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new InvoiceParties(db, new FixedTenant(tenant)).GetCondominiumAsync(Ct);
    }

    private async Task<IReadOnlyDictionary<Guid, InvoiceResponsible>> GetResponsiblesAsync(Guid tenant, IReadOnlyCollection<Guid> unitIds)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new InvoiceParties(db, new FixedTenant(tenant)).GetResponsiblesAsync(unitIds, Today, Ct);
    }
}
