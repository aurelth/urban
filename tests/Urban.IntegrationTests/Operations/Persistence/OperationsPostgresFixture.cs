using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Urban.Operations.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Operations.Persistence;

/// <summary>
/// Um PostgreSQL real (mesma versão da produção) por execução de testes, com as migrations do módulo aplicadas uma única vez.
/// Os testes compartilham o banco e se isolam usando condomínios (tenants) únicos.
/// </summary>
public sealed class OperationsPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using OperationsDbContext db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Cria um contexto novo, como se a requisição pertencesse ao condomínio dado (nulo = sem condomínio).</summary>
    public OperationsDbContext CreateContext(Guid? tenant = null) =>
        new(OperationsPersistence.CreateOptions(ConnectionString), new FixedTenant(tenant));
}

[CollectionDefinition(Name)]
public sealed class OperationsPostgresCollection : ICollectionFixture<OperationsPostgresFixture>
{
    public const string Name = "Operations Postgres";
}
