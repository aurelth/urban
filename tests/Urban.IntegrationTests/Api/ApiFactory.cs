using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Urban.Identity.Infrastructure.Security;

namespace Urban.IntegrationTests.Api;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string UnusedPostgres = "Host=localhost;Database=unused;Username=unused;Password=unused";

    public const string UnusedRedis = "localhost:6379";

    public string PostgresConnectionString { get; init; } = UnusedPostgres;

    public string RedisConnectionString { get; init; } = UnusedRedis;

    public string EnvironmentName { get; init; } = "Development";

    public bool MigrateOnStartup { get; init; }

    public string SigningKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Database:MigrateOnStartup", MigrateOnStartup ? "true" : "false");

        builder.UseSetting(
            "PasswordHashing:WorkFactor",
            PasswordHashingOptions.MinimumWorkFactor.ToString(CultureInfo.InvariantCulture));
    }
}