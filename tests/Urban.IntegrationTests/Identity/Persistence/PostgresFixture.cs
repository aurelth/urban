using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

/// <summary>
/// Um PostgreSQL real (mesma versão da produção) por execução de testes, com as migrations aplicadas uma única vez.
/// Os testes compartilham o banco e se isolam usando identificadores únicos.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using IdentityDbContext db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Cria um contexto novo. Ler com outro contexto garante que o dado veio do banco e não do rastreamento.</summary>
    public IdentityDbContext CreateContext() => new(IdentityPersistence.CreateOptions(_container.GetConnectionString()));

    /// <summary>Grava um usuário com e-mail único e devolve o que foi gravado.</summary>
    public async Task<User> SeedUserAsync(AccountStatus status = AccountStatus.Active, UserRole role = UserRole.Resident)
    {
        User user = UserBuilder.InStatus(status, role, $"{Guid.NewGuid():N}@example.com");

        await using IdentityDbContext db = CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
