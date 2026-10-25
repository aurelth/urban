using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urban.Identity.Infrastructure.Security;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string UnusedPostgres = "Host=localhost;Database=unused;Username=unused;Password=unused";

    public const string UnusedRedis = "localhost:6379";

    public const string DownRedis = "127.0.0.1:1,connectTimeout=500,syncTimeout=500,asyncTimeout=500";

    public string PostgresConnectionString { get; init; } = UnusedPostgres;

    public string RedisConnectionString { get; init; } = UnusedRedis;

    public string EnvironmentName { get; init; } = "Development";

    public bool MigrateOnStartup { get; init; }

    public string WebBaseUrl { get; init; } = "https://app.test";

    public string EmailProvider { get; init; } = "Log";

    public string BrevoApiKey { get; init; } = string.Empty;

    public RecordingEmailSender Emails { get; } = new();

    public string SigningKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Database:MigrateOnStartup", MigrateOnStartup ? "true" : "false");
        builder.UseSetting("Links:WebBaseUrl", WebBaseUrl);
        builder.UseSetting("Email:Provider", EmailProvider);
        builder.UseSetting("Email:Brevo:ApiKey", BrevoApiKey);
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails)));

        builder.UseSetting(
            "PasswordHashing:WorkFactor",
            PasswordHashingOptions.MinimumWorkFactor.ToString(CultureInfo.InvariantCulture));
    }
}