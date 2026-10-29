using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumRegistrationsTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_cnpj_that_was_never_saved_does_not_exist()
    {
        (await ExistsAsync(CondominiumBuilders.UniqueCnpj())).Should().BeFalse();
    }

    [Fact]
    public async Task A_saved_cnpj_exists_whatever_the_tenant_of_the_request()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        (await SaveAsync(condominium, CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy))).IsSuccess.Should().BeTrue();

        (await ExistsAsync(condominium.Cnpj)).Should().BeTrue();
        (await ExistsAsync(condominium.Cnpj, tenant: Guid.CreateVersion7())).Should().BeTrue("o CNPJ é único na plataforma inteira, não só no tenant");
    }

    [Fact]
    public async Task A_manager_with_no_condominium_is_reported_as_such()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        (await new CondominiumRegistrations(db).ManagerHasCondominiumAsync(Guid.CreateVersion7(), Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_manager_who_registered_a_condominium_is_reported_as_having_one()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        await SaveAsync(condominium, CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy));

        await using CondominiumDbContext db = postgres.CreateContext();
        (await new CondominiumRegistrations(db).ManagerHasCondominiumAsync(condominium.CreatedBy, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Saving_stores_the_condominium_and_the_manager_together()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person manager = CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy);

        Result result = await SaveAsync(condominium, manager);

        result.IsSuccess.Should().BeTrue();
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        (await db.Condominiums.CountAsync(c => c.Id == condominium.Id, Ct)).Should().Be(1);
        Person stored = await db.People.SingleAsync(p => p.UserId == condominium.CreatedBy, Ct);
        stored.CondominiumId.Should().Be(condominium.Id);
        stored.Cpf.Value.Should().Be(manager.Cpf.Value);
    }

    [Fact]
    public async Task Saving_a_second_condominium_with_the_same_cnpj_is_a_conflict_and_stores_nothing_of_it()
    {
        Cnpj cnpj = CondominiumBuilders.UniqueCnpj();
        CondominiumEntity first = CondominiumBuilders.Condominium(cnpj: cnpj);
        await SaveAsync(first, CondominiumBuilders.Manager(first.Id, first.CreatedBy));
        CondominiumEntity second = CondominiumBuilders.Condominium(cnpj: cnpj);
        Person secondManager = CondominiumBuilders.Manager(second.Id, second.CreatedBy);

        Result result = await SaveAsync(second, secondManager);

        result.Error.Should().Be(CondominiumErrors.CnpjAlreadyRegistered);
        await using CondominiumDbContext db = postgres.CreateContext();
        (await db.Condominiums.CountAsync(c => c.Id == second.Id, Ct)).Should().Be(0);
        (await db.People.IgnoreQueryFilters().CountAsync(p => p.Id == secondManager.Id, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Saving_a_second_condominium_for_the_same_manager_is_a_conflict()
    {
        CondominiumEntity first = CondominiumBuilders.Condominium();
        await SaveAsync(first, CondominiumBuilders.Manager(first.Id, first.CreatedBy));
        CondominiumEntity second = CondominiumBuilders.Condominium(createdBy: first.CreatedBy);

        Result result = await SaveAsync(second, CondominiumBuilders.Manager(second.Id, second.CreatedBy));

        result.Error.Should().Be(CondominiumErrors.ManagerAlreadyRegistered);
    }

    [Fact]
    public async Task Saving_a_manager_whose_user_already_has_a_person_is_a_conflict()
    {
        CondominiumEntity first = CondominiumBuilders.Condominium();
        Person firstManager = CondominiumBuilders.Manager(first.Id, first.CreatedBy);
        await SaveAsync(first, firstManager);
        CondominiumEntity second = CondominiumBuilders.Condominium();

        Result result = await SaveAsync(second, CondominiumBuilders.Manager(second.Id, firstManager.UserId));

        result.Error.Should().Be(CondominiumErrors.ManagerAlreadyRegistered);
    }

    [Fact]
    public async Task After_a_conflict_the_context_is_left_clean_and_can_save_something_else()
    {
        CondominiumEntity first = CondominiumBuilders.Condominium();
        await SaveAsync(first, CondominiumBuilders.Manager(first.Id, first.CreatedBy));
        CondominiumEntity duplicate = CondominiumBuilders.Condominium(cnpj: first.Cnpj);
        await using CondominiumDbContext db = postgres.CreateContext();
        CondominiumRegistrations registrations = new(db);
        (await registrations.SaveAsync(duplicate, CondominiumBuilders.Manager(duplicate.Id, duplicate.CreatedBy), Ct)).IsFailure.Should().BeTrue();

        db.ChangeTracker.Entries().Should().BeEmpty("a gravação que falhou não pode ficar pendente para a próxima");

        CondominiumEntity another = CondominiumBuilders.Condominium();
        (await registrations.SaveAsync(another, CondominiumBuilders.Manager(another.Id, another.CreatedBy), Ct)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Failures_that_are_not_business_conflicts_are_not_swallowed()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person orphan = CondominiumBuilders.Manager(Guid.CreateVersion7(), condominium.CreatedBy);

        Func<Task> save = () => SaveAsync(condominium, orphan);

        await save.Should().ThrowAsync<DbUpdateException>("pessoa de um condomínio que não existe é defeito do programa, não conflito de negócio");
    }

    private async Task<bool> ExistsAsync(Cnpj cnpj, Guid? tenant = null)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);

        return await new CondominiumRegistrations(db).CnpjExistsAsync(cnpj, Ct);
    }

    private async Task<Result> SaveAsync(CondominiumEntity condominium, Person manager)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        return await new CondominiumRegistrations(db).SaveAsync(condominium, manager, Ct);
    }
}
