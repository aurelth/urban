using Microsoft.EntityFrameworkCore;

namespace Urban.Operations.Infrastructure.Persistence;

/// <summary>Configuração única do EF Core do módulo, usada pela aplicação, pelos testes e pela ferramenta de migrations.</summary>
public static class OperationsPersistence
{
    /// <summary>Cada módulo guarda o histórico de migrations no próprio schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, OperationsDbContext.Schema))
            .UseSnakeCaseNamingConvention();

    public static DbContextOptions<OperationsDbContext> CreateOptions(string connectionString)
    {
        DbContextOptionsBuilder<OperationsDbContext> builder = new();
        Configure(builder, connectionString);
        return builder.Options;
    }
}
