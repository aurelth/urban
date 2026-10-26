using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.Identity.Infrastructure.Security;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

/// <summary>
/// Um PostgreSQL real (mesma versão da produção) por execução de testes, com as migrations aplicadas uma única vez.
/// Os testes compartilham o banco e se isolam usando identificadores únicos.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>BCrypt com o custo mínimo: os testes verificam as regras, não a lentidão do algoritmo.</summary>
    public static readonly IPasswordHasher Hasher = new PasswordHasher(new PasswordHashingOptions { WorkFactor = PasswordHashingOptions.MinimumWorkFactor });

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using IdentityDbContext db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Cria um contexto novo. Ler com outro contexto garante que o dado veio do banco e não do rastreamento.</summary>
    public IdentityDbContext CreateContext() => new(IdentityPersistence.CreateOptions(ConnectionString));

    /// <summary>Grava um usuário com e-mail único e devolve o que foi gravado.</summary>
    public async Task<User> SeedUserAsync(AccountStatus status = AccountStatus.Active, UserRole role = UserRole.Resident)
    {
        User user = UserBuilder.InStatus(status, role, $"{Guid.NewGuid():N}@example.com");

        await using IdentityDbContext db = CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    /// <summary>Grava um usuário (ativo, por padrão) junto com a credencial dele (senha em BCrypt) e devolve o usuário.</summary>
    public async Task<User> SeedUserWithPasswordAsync(
        string password,
        AccountStatus status = AccountStatus.Active,
        UserRole role = UserRole.Resident)
    {
        User user = UserBuilder.InStatus(status, role, $"{Guid.NewGuid():N}@example.com");

        await using IdentityDbContext db = CreateContext();
        db.Users.Add(user);
        db.UserCredentials.Add(UserCredential.Create(user.Id, Hasher.Hash(password)));
        await db.SaveChangesAsync();

        return user;
    }

    /// <summary>Lê a credencial direto do banco, com um contexto novo.</summary>
    public async Task<UserCredential?> ReadCredentialAsync(Guid userId)
    {
        await using IdentityDbContext db = CreateContext();

        return await db.UserCredentials.AsNoTracking().SingleOrDefaultAsync(c => c.UserId == userId);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}