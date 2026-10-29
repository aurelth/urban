using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>O cadastro de ponta a ponta pela API real (MediatR, os dois módulos e o PostgreSQL), sem passar pelo HTTP.</summary>
[Collection(ApiCollection.Name)]
public sealed class RegisterCondominiumFlowTests(ApiFixture fixture) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    public Task InitializeAsync() => fixture.Factory.Services.MigrateCondominiumAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_manager_registers_the_condominium_and_ends_up_linked_with_a_display_name()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();
        RegisterCondominiumCommand command = CondominiumCommands.Valid(manager.Id, cpf: "529.982.247-25");

        Result<RegisteredCondominium> result = await SendAsync(command);

        result.IsSuccess.Should().BeTrue();
        RegisteredCondominium registered = result.Value;
        registered.ManagerCpf.Should().Be("***.982.247-**");
        (await ReadUserAsync(manager.Id)).CondominiumId.Should().Be(registered.CondominiumId);
        (await ReadUserAsync(manager.Id)).DisplayName!.Value.Should().Be("Ana");
        (await ReadCondominiumAsync(registered.CondominiumId))!.CreatedBy.Should().Be(manager.Id);
        Person person = (await ReadPersonAsync(manager.Id))!;
        person.CondominiumId.Should().Be(registered.CondominiumId);
        person.Cpf.Value.Should().Be("52998224725");
    }

    [Fact]
    public async Task A_cnpj_already_registered_by_another_manager_is_refused_and_leaves_the_second_manager_untouched()
    {
        User first = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();
        User second = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();
        string cnpj = CondominiumBuilders.UniqueCnpj().Value;
        (await SendAsync(CondominiumCommands.Valid(first.Id, cnpj))).IsSuccess.Should().BeTrue();

        Result<RegisteredCondominium> result = await SendAsync(CondominiumCommands.Valid(second.Id, cnpj));

        result.Error.Should().Be(CondominiumErrors.CnpjAlreadyRegistered);
        (await ReadUserAsync(second.Id)).CondominiumId.Should().BeNull();
        (await ReadPersonAsync(second.Id)).Should().BeNull();
        (await CountCondominiumsOfAsync(second.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_manager_cannot_register_a_second_condominium()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();
        RegisteredCondominium first = (await SendAsync(CondominiumCommands.Valid(manager.Id))).Value;

        Result<RegisteredCondominium> result = await SendAsync(CondominiumCommands.Valid(manager.Id));

        result.Error.Should().Be(CondominiumErrors.ManagerAlreadyRegistered);
        (await CountCondominiumsOfAsync(manager.Id)).Should().Be(1);
        (await ReadUserAsync(manager.Id)).CondominiumId.Should().Be(first.CondominiumId);
    }

    [Fact]
    public async Task When_identity_refuses_the_enrollment_the_condominium_and_the_person_are_rolled_back()
    {
        User resident = await fixture.Postgres.SeedUserAsync(AccountStatus.Active, UserRole.Resident);

        Result<RegisteredCondominium> result = await SendAsync(CondominiumCommands.Valid(resident.Id));

        result.Error.Should().Be(UserErrors.NotManager);
        (await CountCondominiumsOfAsync(resident.Id)).Should().Be(0);
        (await ReadPersonAsync(resident.Id)).Should().BeNull();
    }

    [Fact]
    public async Task An_invalid_display_name_rolls_everything_back()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();

        Result<RegisteredCondominium> result = await SendAsync(CondominiumCommands.Valid(manager.Id) with { DisplayName = "1" });

        result.Error.Should().Be(DisplayName.Invalid);
        (await CountCondominiumsOfAsync(manager.Id)).Should().Be(0);
        (await ReadUserAsync(manager.Id)).CondominiumId.Should().BeNull();
    }

    [Fact]
    public async Task An_account_that_cannot_sign_in_rolls_everything_back()
    {
        User pending = await fixture.Postgres.SeedManagerWithoutCondominiumAsync(AccountStatus.PendingVerification);

        Result<RegisteredCondominium> result = await SendAsync(CondominiumCommands.Valid(pending.Id));

        result.Error.Should().Be(UserErrors.EmailNotVerified);
        (await CountCondominiumsOfAsync(pending.Id)).Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_account_is_refused_and_nothing_is_stored()
    {
        Guid unknown = Guid.CreateVersion7();

        Result<RegisteredCondominium> result = await SendAsync(CondominiumCommands.Valid(unknown));

        result.Error.Should().Be(SessionErrors.Unauthenticated);
        (await CountCondominiumsOfAsync(unknown)).Should().Be(0);
        (await ReadPersonAsync(unknown)).Should().BeNull();
    }

    [Fact]
    public async Task Two_managers_racing_for_the_same_cnpj_end_with_exactly_one_winner()
    {
        User a = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();
        User b = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();
        string cnpj = CondominiumBuilders.UniqueCnpj().Value;

        Result<RegisteredCondominium>[] results = await Task.WhenAll(
            SendAsync(CondominiumCommands.Valid(a.Id, cnpj)),
            SendAsync(CondominiumCommands.Valid(b.Id, cnpj)));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Should().Be(CondominiumErrors.CnpjAlreadyRegistered);
        (await CountCondominiumsOfAsync(a.Id) + await CountCondominiumsOfAsync(b.Id)).Should().Be(1);
        int linked = new[] { await ReadUserAsync(a.Id), await ReadUserAsync(b.Id) }.Count(u => u.CondominiumId is not null);
        linked.Should().Be(1, "só quem ganhou foi vinculado");
    }

    [Fact]
    public async Task The_same_manager_racing_with_himself_registers_only_one_condominium()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();

        Result<RegisteredCondominium>[] results = await Task.WhenAll(
            SendAsync(CondominiumCommands.Valid(manager.Id)),
            SendAsync(CondominiumCommands.Valid(manager.Id)));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Should().Be(CondominiumErrors.ManagerAlreadyRegistered);
        (await CountCondominiumsOfAsync(manager.Id)).Should().Be(1);
    }

    [Fact]
    public async Task An_invalid_cpf_stores_nothing_and_never_reaches_the_database()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumAsync();

        Result<RegisteredCondominium> result = await SendAsync(CondominiumCommands.Valid(manager.Id, cpf: "111.111.111-11"));

        result.Error.Code.Should().Be("condominium.cpf.invalid");
        (await CountCondominiumsOfAsync(manager.Id)).Should().Be(0);
    }

    private async Task<Result<RegisteredCondominium>> SendAsync(RegisterCondominiumCommand command)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
    }

    private async Task<User> ReadUserAsync(Guid id)
    {
        await using Urban.Identity.Infrastructure.Persistence.IdentityDbContext db = fixture.Postgres.CreateContext();

        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
    }

    private async Task<Urban.Condominium.Domain.Condominiums.Condominium?> ReadCondominiumAsync(Guid id)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<CondominiumDbContext>().Condominiums.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, Ct);
    }

    private async Task<Person?> ReadPersonAsync(Guid userId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<CondominiumDbContext>().People.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, Ct);
    }

    private async Task<int> CountCondominiumsOfAsync(Guid managerUserId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<CondominiumDbContext>().Condominiums.CountAsync(c => c.CreatedBy == managerUserId, Ct);
    }
}
