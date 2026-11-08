using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Urban.Finance.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Finance.Persistence;

/// <summary>
/// Um PostgreSQL real (mesma versão da produção) por execução de testes, com as migrations do módulo aplicadas uma única vez.
/// Os testes compartilham o banco e se isolam usando condomínios (tenants) únicos.
/// </summary>
public sealed class FinancePostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using FinanceDbContext db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Cria um contexto novo, como se a requisição pertencesse ao condomínio dado (nulo = sem condomínio).</summary>
    public FinanceDbContext CreateContext(Guid? tenant = null) =>
        new(FinancePersistence.CreateOptions(ConnectionString), new FixedTenant(tenant));
}

[CollectionDefinition(Name)]
public sealed class FinancePostgresCollection : ICollectionFixture<FinancePostgresFixture>
{
    public const string Name = "Finance Postgres";
}
