using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Time;

namespace Urban.Finance.Infrastructure;

public static class FinanceInfrastructureExtensions
{
    private const string PostgresConnectionName = "Postgres";

    /// <remarks>Quem hospeda o módulo precisa registrar o <see cref="ITenantContext"/> da sua origem de requisições (a API lê do token).</remarks>
    public static IServiceCollection AddFinanceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string postgres = configuration.GetConnectionString(PostgresConnectionName) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Falta a cadeia de conexão '{PostgresConnectionName}' (ConnectionStrings:{PostgresConnectionName}).");

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddDbContext<FinanceDbContext>(options => FinancePersistence.Configure(options, postgres));
        services.AddScoped<IExpenseGroups, FinanceExpenseGroups>();
        services.AddScoped<IExpenses, FinanceExpenses>();
        services.AddScoped<IExpenseEntries, FinanceExpenseEntries>();
        services.AddScoped<IMonthClosings, FinanceMonthClosings>();

        return services;
    }

    public static async Task MigrateFinanceAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        FinanceDbContext db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
