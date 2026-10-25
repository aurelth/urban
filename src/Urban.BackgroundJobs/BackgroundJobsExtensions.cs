using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Urban.BackgroundJobs;

public static class BackgroundJobsExtensions
{
    private const string PostgresConnectionName = "Postgres";

    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        string? postgres = configuration.GetConnectionString(PostgresConnectionName);
        if (string.IsNullOrWhiteSpace(postgres))
        {
            throw new InvalidOperationException($"Falta a cadeia de conexão '{PostgresConnectionName}' (ConnectionStrings:{PostgresConnectionName}).");
        }

        BackgroundJobsOptions options = configuration.GetSection(BackgroundJobsOptions.SectionName).Get<BackgroundJobsOptions>() ?? new BackgroundJobsOptions();
        options.Validate();

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(postgres), BackgroundJobsOptions.CreateStorageOptions()));

        if (options.RunServer)
        {
            services.AddHangfireServer(options.ApplyTo);
        }

        return services;
    }
}