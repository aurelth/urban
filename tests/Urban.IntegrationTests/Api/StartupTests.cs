using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Identity.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class StartupTests(ApiFixture fixture)
{
    [Fact]
    public async Task Migrates_an_empty_database_on_startup_when_enabled()
    {
        string connectionString = await CreateEmptyDatabaseAsync();
        await using ApiFactory factory = new()
        {
            PostgresConnectionString = connectionString,
            RedisConnectionString = ApiFactory.UnusedRedis,
            MigrateOnStartup = true,
        };

        using HttpClient client = factory.CreateClient();

        await using IdentityDbContext db = new(IdentityPersistence.CreateOptions(connectionString));
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Leaves_the_database_alone_when_migration_is_disabled()
    {
        string connectionString = await CreateEmptyDatabaseAsync();
        await using ApiFactory factory = new()
        {
            PostgresConnectionString = connectionString,
            RedisConnectionString = ApiFactory.UnusedRedis,
            MigrateOnStartup = false,
        };

        using HttpClient client = factory.CreateClient();

        await using IdentityDbContext db = new(IdentityPersistence.CreateOptions(connectionString));
        (await db.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public void Refuses_to_start_with_an_invalid_signing_key()
    {
        using ApiFactory factory = new() { SigningKey = "curta" };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*chave de assinatura*");
    }

    [Theory]
    [InlineData("", ApiFactory.UnusedRedis, "Postgres")]
    [InlineData(ApiFactory.UnusedPostgres, "", "Redis")]
    public void Refuses_to_start_without_a_connection_string(string postgres, string redis, string missing)
    {
        using ApiFactory factory = new() { PostgresConnectionString = postgres, RedisConnectionString = redis };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage($"*'{missing}'*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("urban-pro.io")]
    [InlineData("ftp://urban-pro.io")]
    public void Refuses_to_start_without_a_valid_web_address(string webBaseUrl)
    {
        using ApiFactory factory = new() { WebBaseUrl = webBaseUrl };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*Links:WebBaseUrl*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("versao-com-mais-de-trinta-e-dois-caracteres")]
    public void Refuses_to_start_without_a_valid_terms_version(string termsVersion)
    {
        using ApiFactory factory = new() { TermsVersion = termsVersion };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*Consent:CurrentVersion*");
    }

    [Fact]
    public void Refuses_to_start_without_an_email_provider()
    {
        using ApiFactory factory = new() { EmailProvider = string.Empty };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*Email:Provider*");
    }

    [Fact]
    public void Refuses_to_start_with_the_brevo_provider_and_no_key()
    {
        using ApiFactory factory = new() { EmailProvider = "Brevo" };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*Email:Brevo:ApiKey*");
    }

    [Fact]
    public void Starts_with_the_brevo_provider_and_a_key()
    {
        using ApiFactory factory = new() { EmailProvider = "Brevo", BrevoApiKey = "chave-de-teste" };

        Action start = () => factory.CreateClient();

        start.Should().NotThrow();
    }

    private async Task<string> CreateEmptyDatabaseAsync()
    {
        NpgsqlConnectionStringBuilder builder = new(fixture.Postgres.ConnectionString);
        string database = $"urban_startup_{Guid.NewGuid():N}";

        await using (NpgsqlConnection admin = new(builder.ConnectionString))
        {
            await admin.OpenAsync();
            await using NpgsqlCommand create = new($"create database {database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        builder.Database = database;
        return builder.ConnectionString;
    }
}