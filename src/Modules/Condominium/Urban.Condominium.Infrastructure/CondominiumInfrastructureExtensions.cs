using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Condominium.Infrastructure.Security;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Time;

namespace Urban.Condominium.Infrastructure;

public static class CondominiumInfrastructureExtensions
{
    private const string PostgresConnectionName = "Postgres";

    /// <remarks>Quem hospeda o módulo precisa registrar o <see cref="Urban.SharedKernel.Abstractions.ITenantContext"/> da sua origem de requisições (a API lê do token).</remarks>
    public static IServiceCollection AddCondominiumInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string postgres = configuration.GetConnectionString(PostgresConnectionName) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Falta a cadeia de conexão '{PostgresConnectionName}' (ConnectionStrings:{PostgresConnectionName}).");

        PersonalDataOptions personalData = configuration.GetSection(PersonalDataOptions.SectionName).Get<PersonalDataOptions>() ?? new PersonalDataOptions();
        _ = personalData.GetHashKey(); // confere também as chaves de cifragem: configuração ruim derruba a partida, não o primeiro CPF

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton(personalData);
        services.AddSingleton<IPersonalDataProtector, PersonalDataProtector>();
        services.AddDbContext<CondominiumDbContext>(options => CondominiumPersistence.Configure(options, postgres));
        services.AddScoped<ICondominiumRegistrations, CondominiumRegistrations>();

        return services;
    }

    public static async Task MigrateCondominiumAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CondominiumDbContext db = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
