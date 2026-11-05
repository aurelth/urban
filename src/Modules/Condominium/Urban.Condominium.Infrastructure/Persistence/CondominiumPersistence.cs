using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>Configuração única do EF Core do módulo, usada pela aplicação, pelos testes e pela ferramenta de migrations.</summary>
public static class CondominiumPersistence
{
    /// <summary>Cada módulo guarda o histórico de migrations no próprio schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Collation ICU com ordenação numérica (<c>kn-true</c>): "2" vem antes de "10" e "Casa 2" antes de "Casa 10", sem
    /// diferenciar maiúsculas. Não é determinística, então só serve para ordenar, nunca para igualdade ou índice único.
    /// </summary>
    public const string NaturalCollation = "natural_order";

    /// <summary>
    /// A collation fica no schema <c>public</c> (o primeiro do <c>search_path</c> padrão) porque a ordenação a cita só pelo nome;
    /// no schema do módulo o PostgreSQL não a encontraria.
    /// </summary>
    public const string CollationSchema = "public";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, CondominiumDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, ProtectorAwareModelCacheKeyFactory>();

    public static DbContextOptions<CondominiumDbContext> CreateOptions(string connectionString)
    {
        DbContextOptionsBuilder<CondominiumDbContext> builder = new();
        Configure(builder, connectionString);
        return builder.Options;
    }

    /// <summary>
    /// Os conversores do CPF carregam o protetor dentro do modelo, e o EF guarda o modelo em cache por tipo de contexto.
    /// Incluir o protetor na chave do cache faz cada protetor (cada conjunto de chaves) ter o seu modelo; em produção
    /// há um só protetor, então o modelo é montado uma única vez.
    /// </summary>
    private sealed class ProtectorAwareModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (context.GetType(), (context as CondominiumDbContext)?.Protector, designTime);
    }
}
