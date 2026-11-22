using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Condominium.Application.Abstractions;
using Urban.SharedKernel;
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

        found.Should().NotBeNull();
        found!.Name.Should().Be(condominium.Name);
        found.Cnpj.Should().Be(condominium.Cnpj.Formatted);
        found.Address.Should().Be(condominium.Address.Formatted);
    }

    [Fact]
    public async Task The_manager_who_registered_the_condominium_comes_with_the_name_and_the_formatted_phone()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        InvoiceCondominium? found = await GetCondominiumAsync(condominium.Id);

        found!.Manager.Should().Be(new InvoiceManager("Ana Maria Silva", "(47) 99123-4567"));
    }

    [Fact]
    public async Task Without_the_person_of_the_manager_the_condominium_still_comes_but_with_no_contact()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await using (CondominiumDbContext db = postgres.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from condominium.person where user_id = {condominium.CreatedBy}", Ct);
        }

        InvoiceCondominium? found = await GetCondominiumAsync(condominium.Id);

        found.Should().NotBeNull();
        found!.Manager.Should().BeNull();
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
    public async Task A_manager_who_lives_in_a_unit_is_its_responsible_at_the_email_of_his_account()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        await LiveInAsync(condominium, unit);
        FakeManagerProfile managers = new();
        managers.Has(condominium.CreatedBy, condominium.Id, "sindica@exemplo.com");

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [unit.Id], managers);

        responsibles[unit.Id].Should().Match<InvoiceResponsible>(found => found.Name == "Ana Maria Silva" && found.Email == "sindica@exemplo.com");
    }

    [Fact]
    public async Task A_manager_living_in_several_units_has_his_account_asked_once()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit first = await SeedUnitAsync(condominium);
        CondominiumUnit second = await SeedUnitAsync(condominium);
        await LiveInAsync(condominium, first);
        await LiveInAsync(condominium, second);
        FakeManagerProfile managers = new();
        managers.Has(condominium.CreatedBy, condominium.Id, "sindica@exemplo.com");

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [first.Id, second.Id], managers);

        responsibles.Values.Should().OnlyContain(found => found.Email == "sindica@exemplo.com");
        managers.Asked.Should().Be(1);
    }

    [Fact]
    public async Task A_manager_whose_account_gives_no_email_stays_a_responsible_without_one()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        await LiveInAsync(condominium, unit);
        FakeManagerProfile managers = new();

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [unit.Id], managers);

        responsibles[unit.Id].Should().Match<InvoiceResponsible>(found => found.Name == "Ana Maria Silva" && found.Email == null);
        managers.Asked.Should().Be(1);
    }

    [Fact]
    public async Task A_responsible_with_an_email_on_the_record_never_has_the_account_asked()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person owner = await AddAsync(condominium, unit, "Carlos Proprietario", OccupancyKind.Owner);
        FakeManagerProfile managers = new();

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [unit.Id], managers);

        responsibles[unit.Id].Email.Should().Be(owner.Email!.Value);
        managers.Asked.Should().Be(0);
    }

    [Fact]
    public async Task A_responsible_without_an_email_and_without_an_account_has_no_account_to_ask()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = await SeedUnitAsync(condominium);
        Person person = ResidentBuilders.Resident(condominium.Id, "Sem Email", email: null);
        await AddAsync(condominium, person, ResidentBuilders.Occupancy(person, unit.Id, OccupancyKind.Owner));
        FakeManagerProfile managers = new();

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await GetResponsiblesAsync(condominium.Id, [unit.Id], managers);

        responsibles[unit.Id].Email.Should().BeNull();
        managers.Asked.Should().Be(0);
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

        return await new InvoiceParties(db, new FixedTenant(tenant), new FakeManagerProfile()).GetCondominiumAsync(Ct);
    }

    private async Task<IReadOnlyDictionary<Guid, InvoiceResponsible>> GetResponsiblesAsync(Guid tenant, IReadOnlyCollection<Guid> unitIds, FakeManagerProfile? managers = null)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new InvoiceParties(db, new FixedTenant(tenant), managers ?? new FakeManagerProfile()).GetResponsiblesAsync(unitIds, Today, Ct);
    }

    /// <summary>O síndico do condomínio passa a morar na unidade: a pessoa dele (a que tem a conta) ganha o vínculo de proprietário.</summary>
    private async Task LiveInAsync(CondominiumEntity condominium, CondominiumUnit unit)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Person manager = await db.People.AsNoTracking().SingleAsync(person => person.UserId == condominium.CreatedBy, Ct);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Resident, ["occupancy"], ResidentBuilders.Now, manager.Id);
        (await new CondominiumResidents(db).AddOccupancyAsync(ResidentBuilders.Occupancy(manager, unit.Id, OccupancyKind.Owner), entry, Ct)).IsSuccess.Should().BeTrue();
    }

    /// <summary>A conta do síndico, como o Identity a entrega: só responde por (conta, condomínio) cadastrados, e conta quantas vezes foi perguntada.</summary>
    private sealed class FakeManagerProfile : IManagerProfile
    {
        private readonly Dictionary<(Guid UserId, Guid CondominiumId), string> _emails = [];

        public int Asked { get; private set; }

        public void Has(Guid userId, Guid condominiumId, string email) => _emails[(userId, condominiumId)] = email;

        public Task<string?> GetEmailAsync(Guid userId, Guid condominiumId, CancellationToken cancellationToken)
        {
            Asked++;

            return Task.FromResult(_emails.GetValueOrDefault((userId, condominiumId)));
        }

        public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<string>> UpdateDisplayNameAsync(Guid userId, string? displayName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
