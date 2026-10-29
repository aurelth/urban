using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Condominium.Infrastructure.Security;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>
/// Um PostgreSQL real (mesma versão da produção) por execução de testes, com as migrations do módulo aplicadas uma única vez.
/// Os testes compartilham o banco e se isolam usando identificadores únicos.
/// </summary>
public sealed class CondominiumPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public PersonalDataProtector Protector { get; } = new(new PersonalDataOptions
    {
        CurrentKeyId = "v1",
        EncryptionKeys = { ["v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
        HashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    });

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using CondominiumDbContext db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Cria um contexto novo, como se a requisição pertencesse ao condomínio dado (nulo = quem ainda não tem condomínio).</summary>
    public CondominiumDbContext CreateContext(Guid? tenant = null) =>
        new(CondominiumPersistence.CreateOptions(ConnectionString), Protector, new FixedTenant(tenant));
}

[CollectionDefinition(Name)]
public sealed class CondominiumPostgresCollection : ICollectionFixture<CondominiumPostgresFixture>
{
    public const string Name = "Condominium Postgres";
}
