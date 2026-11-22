using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Infrastructure.Persistence;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Time;

namespace Urban.Operations.Infrastructure;

public static class OperationsInfrastructureExtensions
{
    private const string PostgresConnectionName = "Postgres";

    /// <remarks>Quem hospeda o módulo precisa registrar o <see cref="ITenantContext"/> da sua origem de requisições (a API lê do token) e o contrato <c>IAnnouncementAudience</c>, que o Condomínio cumpre.</remarks>
    public static IServiceCollection AddOperationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string postgres = configuration.GetConnectionString(PostgresConnectionName) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Falta a cadeia de conexão '{PostgresConnectionName}' (ConnectionStrings:{PostgresConnectionName}).");

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddDbContext<OperationsDbContext>(options => OperationsPersistence.Configure(options, postgres));
        services.AddScoped<IAnnouncements, OperationsAnnouncements>();

        return services;
    }

    public static async Task MigrateOperationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        OperationsDbContext db = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
