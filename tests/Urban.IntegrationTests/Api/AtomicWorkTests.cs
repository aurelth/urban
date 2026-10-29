using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Infrastructure;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Condominium;
using Urban.TestSupport.Identity;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class AtomicWorkTests(ApiFixture fixture) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly Error Refusal = new("test.refused", "Recusado de propósito");

    public Task InitializeAsync() => fixture.Factory.Services.MigrateCondominiumAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task When_the_work_succeeds_both_modules_are_committed()
    {
        (User user, CondominiumEntity condominium) = NewPair();
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        Result result = await scope.ServiceProvider.GetRequiredService<IAtomicWork>()
            .ExecuteAsync(ct => WriteBothAsync(scope, user, condominium, ct), Ct);

        result.IsSuccess.Should().BeTrue();
        (await UserExistsAsync(user.Id)).Should().BeTrue();
        (await CondominiumExistsAsync(condominium.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task When_the_work_returns_a_failure_nothing_of_either_module_is_kept_and_the_failure_comes_back()
    {
        (User user, CondominiumEntity condominium) = NewPair();
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        Result result = await scope.ServiceProvider.GetRequiredService<IAtomicWork>().ExecuteAsync(
            async ct =>
            {
                await WriteBothAsync(scope, user, condominium, ct);
                return Result.Failure(Refusal);
            },
            Ct);

        result.Error.Should().Be(Refusal);
        (await UserExistsAsync(user.Id)).Should().BeFalse();
        (await CondominiumExistsAsync(condominium.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task When_the_work_throws_nothing_is_kept_and_the_exception_goes_up()
    {
        (User user, CondominiumEntity condominium) = NewPair();
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        Func<Task> act = () => scope.ServiceProvider.GetRequiredService<IAtomicWork>().ExecuteAsync(
            async ct =>
            {
                await WriteBothAsync(scope, user, condominium, ct);
                throw new InvalidOperationException("falha no meio");
            },
            Ct);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("falha no meio");
        (await UserExistsAsync(user.Id)).Should().BeFalse();
        (await CondominiumExistsAsync(condominium.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Inside_the_work_each_module_sees_what_the_other_wrote_but_nobody_else_does_until_the_commit()
    {
        (User user, CondominiumEntity condominium) = NewPair();
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        bool seenByIdentityContext = false;
        bool seenOutside = true;

        await scope.ServiceProvider.GetRequiredService<IAtomicWork>().ExecuteAsync(
            async ct =>
            {
                await WriteBothAsync(scope, user, condominium, ct);
                IdentityDbContext identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                seenByIdentityContext = await CountCondominiumsAsync(identity, condominium.Id) == 1;
                await using IdentityDbContext outside = fixture.Postgres.CreateContext();
                seenOutside = await CountCondominiumsAsync(outside, condominium.Id) == 1;
                return Result.Success();
            },
            Ct);

        seenByIdentityContext.Should().BeTrue("os dois módulos usam a mesma conexão e a mesma transação");
        seenOutside.Should().BeFalse("antes do commit ninguém de fora enxerga a gravação");
        (await CondominiumExistsAsync(condominium.Id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task After_the_work_both_contexts_of_the_scope_keep_working_and_a_second_unit_can_run(bool firstSucceeds)
    {
        (User user, CondominiumEntity condominium) = NewPair();
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        IAtomicWork atomic = scope.ServiceProvider.GetRequiredService<IAtomicWork>();

        await atomic.ExecuteAsync(
            async ct =>
            {
                await WriteBothAsync(scope, user, condominium, ct);
                return firstSucceeds ? Result.Success() : Result.Failure(Refusal);
            },
            Ct);

        IdentityDbContext identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        CondominiumDbContext condominiums = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();
        (await identity.Users.CountAsync(u => u.Id == user.Id, Ct)).Should().Be(firstSucceeds ? 1 : 0);
        (await condominiums.Condominiums.CountAsync(c => c.Id == condominium.Id, Ct)).Should().Be(firstSucceeds ? 1 : 0);

        (User other, CondominiumEntity otherCondominium) = NewPair();
        Result second = await atomic.ExecuteAsync(ct => WriteBothAsync(scope, other, otherCondominium, ct), Ct);

        second.IsSuccess.Should().BeTrue();
        (await UserExistsAsync(other.Id)).Should().BeTrue();
        (await CondominiumExistsAsync(otherCondominium.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Each_context_gets_its_own_complete_connection_string_back_password_included()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        IdentityDbContext identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        CondominiumDbContext condominiums = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();
        string? identityBefore = identity.Database.GetConnectionString();
        string? condominiumBefore = condominiums.Database.GetConnectionString();

        await scope.ServiceProvider.GetRequiredService<IAtomicWork>().ExecuteAsync(_ => Task.FromResult(Result.Success()), Ct);

        identity.Database.GetConnectionString().Should().Be(identityBefore);
        condominiums.Database.GetConnectionString().Should().Be(condominiumBefore);
        condominiumBefore.Should().Be(fixture.Postgres.ConnectionString);
    }

    [Fact]
    public async Task After_the_work_the_contexts_no_longer_share_a_connection_or_a_transaction()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        IdentityDbContext identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        CondominiumDbContext condominiums = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();

        await scope.ServiceProvider.GetRequiredService<IAtomicWork>().ExecuteAsync(_ => Task.FromResult(Result.Success()), Ct);

        condominiums.Database.GetDbConnection().Should().NotBeSameAs(identity.Database.GetDbConnection());
        condominiums.Database.CurrentTransaction.Should().BeNull();
        identity.Database.CurrentTransaction.Should().BeNull();
    }

    private static (User User, CondominiumEntity Condominium) NewPair()
    {
        User user = UserBuilder.ManagerWithoutCondominium(address: $"{Guid.NewGuid():N}@example.com");

        return (user, CondominiumBuilders.Condominium(createdBy: user.Id));
    }

    private static async Task<Result> WriteBothAsync(AsyncServiceScope scope, User user, CondominiumEntity condominium, CancellationToken ct)
    {
        IdentityDbContext identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        CondominiumDbContext condominiums = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();

        identity.Users.Add(user);
        await identity.SaveChangesAsync(ct);
        condominiums.Condominiums.Add(condominium);
        await condominiums.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static Task<int> CountCondominiumsAsync(IdentityDbContext db, Guid id) =>
        db.Database.SqlQuery<int>($"""select count(*)::int as "Value" from condominium.condominium where id = {id}""").SingleAsync(Ct);

    private async Task<bool> UserExistsAsync(Guid id)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();

        return await db.Users.AnyAsync(u => u.Id == id, Ct);
    }

    private async Task<bool> CondominiumExistsAsync(Guid id)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();

        return await CountCondominiumsAsync(db, id) == 1;
    }
}
