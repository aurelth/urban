using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Infrastructure.Messaging;
using Urban.Identity.Infrastructure.Persistence;
using Urban.Identity.Infrastructure.Security;
using Urban.Identity.Infrastructure.Sessions;
using Urban.Identity.Infrastructure.Tokens;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Time;

namespace Urban.Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    private const string PostgresConnectionName = "Postgres";
    private const string RedisConnectionName = "Redis";

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string postgres = RequireConnectionString(configuration, PostgresConnectionName);
        string redis = RequireConnectionString(configuration, RedisConnectionName);

        JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        _ = jwt.GetSigningKeyBytes();
        PasswordHashingOptions hashing = configuration.GetSection(PasswordHashingOptions.SectionName).Get<PasswordHashingOptions>()
            ?? new PasswordHashingOptions();

        WebLinksOptions links = configuration.GetSection(WebLinksOptions.SectionName).Get<WebLinksOptions>() ?? new WebLinksOptions();
        _ = links.GetWebBaseUri();

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton(jwt);
        services.AddSingleton(hashing);
        services.AddSingleton(links);
        services.AddDbContext<IdentityDbContext>(options => IdentityPersistence.Configure(options, postgres));
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectToRedis(redis));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<ISecretGenerator, SecretGenerator>();
        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerifications, EmailVerifications>();
        services.AddScoped<IEmailVerificationNotifier, EmailVerificationNotifier>();
        services.AddScoped<IEmailVerificationDispatcher, HangfireEmailVerificationDispatcher>();
        services.AddScoped<SendEmailVerificationJob>();
        services.AddScoped<IPasswordResets, PasswordResets>();
        services.AddScoped<IPasswordResetNotifier, PasswordResetNotifier>();
        services.AddScoped<IPasswordResetDispatcher, HangfirePasswordResetDispatcher>();
        services.AddScoped<SendPasswordResetJob>();

        services.AddSingleton<RedisSessionRevocations>();
        services.AddSingleton<ISessionRevocations>(sp => sp.GetRequiredService<RedisSessionRevocations>());
        services.AddSingleton<ISessionRevocationChecker>(sp => sp.GetRequiredService<RedisSessionRevocations>());

        return services;
    }

    public static async Task MigrateIdentityAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static string RequireConnectionString(IConfiguration configuration, string name)
    {
        string? value = configuration.GetConnectionString(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Falta a cadeia de conexão '{name}' (ConnectionStrings:{name}).")
            : value;
    }

    private static ConnectionMultiplexer ConnectToRedis(string connectionString)
    {
        // Sem AbortOnConnectFail a API sobe com o Redis fora do ar; as consultas de revogação falham fechadas.
        ConfigurationOptions options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    }
}