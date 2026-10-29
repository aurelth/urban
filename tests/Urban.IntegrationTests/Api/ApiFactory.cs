using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urban.Identity.Application.Abstractions;
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

    /// <summary>Versão vigente dos termos de uso e da política de privacidade (seção "Consent").</summary>
    public string TermsVersion { get; init; } = "2026-01-01";

    public string EmailProvider { get; init; } = "Log";

    public string BrevoApiKey { get; init; } = string.Empty;

    /// <summary>E-mail do SuperAdmin que a API cria na partida. Vazio (padrão) desliga o bootstrap.</summary>
    public string SuperAdminEmail { get; init; } = string.Empty;

    /// <summary>Liga a fila e o servidor do Hangfire de verdade. Desligado (padrão), o envio roda na hora e nada toca o banco da fila.</summary>
    public bool RealBackgroundJobs { get; init; }

    /// <summary>Limite de requisições de cada rota por janela. O padrão é alto para os testes de outros assuntos nunca esbarrarem nele; nulo usa os limites do appsettings.json.</summary>
    public int? RateLimitPermitLimit { get; init; } = 10_000;

    public TimeSpan RateLimitWindow { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Endereço de quem abre a conexão com a API (o último proxy). Nulo (padrão), o servidor de teste não informa nenhum.</summary>
    public IPAddress? RemoteAddress { get; init; }

    /// <summary>Configurações extras, aplicadas por último e por isso capazes de sobrescrever qualquer outra.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    public RecordingEmailSender Emails { get; } = new();

    public string SigningKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    /// <summary>Chave (versão "v1") que cifra os dados pessoais; 32 bytes em base64.</summary>
    public string PersonalDataEncryptionKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>Chave do hash que garante a unicidade do CPF; no mínimo 32 bytes em base64.</summary>
    public string PersonalDataHashKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("PersonalData:CurrentKeyId", "v1");
        builder.UseSetting("PersonalData:EncryptionKeys:v1", PersonalDataEncryptionKey);
        builder.UseSetting("PersonalData:HashKey", PersonalDataHashKey);
        builder.UseSetting("Database:MigrateOnStartup", MigrateOnStartup ? "true" : "false");
        builder.UseSetting("Links:WebBaseUrl", WebBaseUrl);
        builder.UseSetting("Consent:CurrentVersion", TermsVersion);
        builder.UseSetting("Email:Provider", EmailProvider);
        builder.UseSetting("Email:Brevo:ApiKey", BrevoApiKey);
        builder.UseSetting("SuperAdmin:Email", SuperAdminEmail);
        builder.UseSetting("BackgroundJobs:RunServer", RealBackgroundJobs ? "true" : "false");
        if (RateLimitPermitLimit is int permits)
        {
            foreach (string route in new[] { "Register", "ConfirmEmail", "Login", "ForgotPassword", "ResetPassword", "Refresh", "Logout", "Me", "Terms", "CondominiumRegistration" })
            {
                builder.UseSetting($"RateLimiting:{route}:PermitLimit", permits.ToString(CultureInfo.InvariantCulture));
                builder.UseSetting($"RateLimiting:{route}:Window", RateLimitWindow.ToString("c", CultureInfo.InvariantCulture));
            }
        }

        builder.ConfigureTestServices(services =>
        {
            if (RemoteAddress is not null)
            {
                services.AddSingleton<IStartupFilter>(new RemoteAddressStartupFilter(RemoteAddress));
            }

            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails));
            if (!RealBackgroundJobs)
            {
                services.Replace(ServiceDescriptor.Scoped<IEmailVerificationDispatcher, InlineEmailVerificationDispatcher>());
                services.Replace(ServiceDescriptor.Scoped<IPasswordResetDispatcher, InlinePasswordResetDispatcher>());
            }
        });

        builder.UseSetting(
            "PasswordHashing:WorkFactor",
            PasswordHashingOptions.MinimumWorkFactor.ToString(CultureInfo.InvariantCulture));

        foreach ((string key, string? value) in Settings)
        {
            builder.UseSetting(key, value);
        }
    }
}