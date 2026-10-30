using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Application.Profile;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Infrastructure;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>A edição dos dados de ponta a ponta (MediatR, os dois módulos e o PostgreSQL), sem passar pelo HTTP.</summary>
[Collection(ApiCollection.Name)]
public sealed class MyCondominiumFlowTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string Password = "Senha-segura-123";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private Guid? _condominiumId;

    public Task InitializeAsync() => fixture.Factory.Services.MigrateCondominiumAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_read_change_the_free_fields_read_again_then_change_the_identity_and_read_again()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        (await SendAsync(CondominiumCommands.Valid(manager.Id, cpf: "52998224725"))).IsSuccess.Should().BeTrue();
        await ActAsAsync(manager.Id);

        MyCondominium first = (await SendAsync(new GetMyCondominiumQuery(manager.Id))).Value;
        MyCondominium afterFree = (await SendAsync(new UpdateMyCondominiumCommand(
            manager.Id,
            first.Version,
            "Aninha",
            "(47) 98888-7766",
            ProfileInput.Address(first.Manager.Address),
            "Vila Nova",
            ProfileInput.Address(first.Condominium.Address)))).Value;
        MyCondominium afterIdentity = (await SendAsync(new UpdateManagerIdentityCommand(
            manager.Id,
            afterFree.Version,
            Password,
            "Maria Souza Lima",
            new DateOnly(1990, 7, 1),
            "11144477735"))).Value;

        first.Manager.MaskedCpf.Should().Be("***.982.247-**");
        afterFree.DisplayName.Should().Be("Aninha");
        afterFree.Manager.Phone.Should().Be("47988887766");
        afterFree.Condominium.Name.Should().Be("Vila Nova");
        afterFree.Version.Should().NotBe(first.Version);
        afterIdentity.Manager.FullName.Should().Be("Maria Souza Lima");
        afterIdentity.Manager.MaskedCpf.Should().Be("***.444.777-**");
        afterIdentity.Condominium.Cnpj.Should().Be(first.Condominium.Cnpj);

        // O comando de identidade não conhece o nome de exibição (mora na conta); o endpoint completa lendo de novo.
        MyCondominium reread = (await SendAsync(new GetMyCondominiumQuery(manager.Id))).Value;
        reread.DisplayName.Should().Be("Aninha");
        reread.Should().BeEquivalentTo(afterIdentity, options => options.Excluding(m => m.DisplayName));
    }

    [Fact]
    public async Task When_identity_refuses_the_display_name_the_condominium_and_the_person_changes_are_rolled_back()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        (await SendAsync(CondominiumCommands.Valid(manager.Id))).IsSuccess.Should().BeTrue();
        await ActAsAsync(manager.Id);
        MyCondominium before = (await SendAsync(new GetMyCondominiumQuery(manager.Id))).Value;
        await SuspendAsync(manager.Id);

        Result<MyCondominium> result = await SendAsync(new UpdateMyCondominiumCommand(
            manager.Id,
            before.Version,
            "Aninha",
            "(47) 98888-7766",
            ProfileInput.Address(before.Manager.Address),
            "Vila Nova",
            ProfileInput.Address(before.Condominium.Address)));

        result.Error.Should().Be(UserErrors.AccountSuspended);
        (await SendAsync(new GetMyCondominiumQuery(manager.Id))).Value.Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Two_changes_made_from_the_same_version_end_with_exactly_one_winner()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        (await SendAsync(CondominiumCommands.Valid(manager.Id))).IsSuccess.Should().BeTrue();
        await ActAsAsync(manager.Id);
        MyCondominium before = (await SendAsync(new GetMyCondominiumQuery(manager.Id))).Value;
        UpdateMyCondominiumCommand Change(string phone) => new(
            manager.Id,
            before.Version,
            before.DisplayName,
            phone,
            ProfileInput.Address(before.Manager.Address),
            before.Condominium.Name,
            ProfileInput.Address(before.Condominium.Address));

        Result<MyCondominium>[] results = await Task.WhenAll(SendAsync(Change("47977776666")), SendAsync(Change("47955554444")));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
    }

    /// <summary>
    /// Faz as próximas chamadas valerem como as de uma requisição do síndico: o condomínio do token (claim) é o que alimenta o
    /// filtro de tenant, como acontece na API de verdade.
    /// </summary>
    private async Task ActAsAsync(Guid userId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        CondominiumDbContext db = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();
        Guid condominiumId = await db.People.IgnoreQueryFilters().Where(p => p.UserId == userId).Select(p => p.CondominiumId).SingleAsync(Ct);
        _condominiumId = condominiumId;
    }

    private async Task<Result<T>> SendAsync<T>(IRequest<Result<T>> request)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        if (_condominiumId is Guid condominiumId)
        {
            // O acessor guarda o contexto em AsyncLocal: precisa ser definido no mesmo fluxo que envia o comando.
            ClaimsIdentity identity = new([new Claim(AccessTokenClaims.CondominiumId, condominiumId.ToString())], "teste");
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        }

        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, Ct);
    }

    private async Task SuspendAsync(Guid userId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.SingleAsync(u => u.Id == userId, Ct);
        user.Suspend(Guid.CreateVersion7(), "Motivo de teste", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Ct);
    }
}

internal static class ProfileInput
{
    public static AddressInput Address(AddressView view) =>
        new(view.PostalCode, view.Street, view.Number, view.Complement, view.Neighborhood, view.City, view.State);
}
