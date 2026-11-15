using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>
/// O e-mail passou a ser único por condomínio. Quem já testou o sistema pode ter e-mails repetidos: a migration limpa as repetições
/// (fica o do morador que já tem conta e, entre os demais, o mais antigo) antes de criar o índice, senão o deploy quebraria.
/// </summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class UniqueResidentEmailMigrationTests(CondominiumPostgresFixture postgres)
{
    private const string BeforeTheIndex = "20261114070731_AddVehicles";

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task The_migration_clears_the_repeated_emails_keeping_the_account_holder_then_the_oldest_and_creates_the_unique_index()
    {
        await using CondominiumDbContext db = await CreateDatabaseAtAsync(BeforeTheIndex);
        Guid condominium = await InsertCondominiumAsync(db);
        Guid other = await InsertCondominiumAsync(db);

        Guid oldWithoutAccount = await InsertPersonAsync(db, condominium, "Ana@exemplo.com", createdAt: "2026-01-01");
        Guid newWithAccount = await InsertPersonAsync(db, condominium, "ana@exemplo.com", createdAt: "2026-03-01", userId: Guid.CreateVersion7());
        Guid thirdAna = await InsertPersonAsync(db, condominium, "ANA@EXEMPLO.COM", createdAt: "2026-02-01");
        Guid olderBia = await InsertPersonAsync(db, condominium, "bia@exemplo.com", createdAt: "2026-01-01");
        Guid newerBia = await InsertPersonAsync(db, condominium, "BIA@exemplo.com", createdAt: "2026-01-02");
        Guid solo = await InsertPersonAsync(db, condominium, "solo@exemplo.com", createdAt: "2026-01-01");
        Guid noEmailOne = await InsertPersonAsync(db, condominium, null, createdAt: "2026-01-01");
        Guid noEmailTwo = await InsertPersonAsync(db, condominium, null, createdAt: "2026-01-01");
        Guid inAnotherCondominium = await InsertPersonAsync(db, other, "ana@exemplo.com", createdAt: "2026-01-01");

        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        (await EmailOfAsync(db, newWithAccount)).Should().Be("ana@exemplo.com");
        (await EmailOfAsync(db, oldWithoutAccount)).Should().BeNull();
        (await EmailOfAsync(db, thirdAna)).Should().BeNull();
        (await EmailOfAsync(db, olderBia)).Should().Be("bia@exemplo.com");
        (await EmailOfAsync(db, newerBia)).Should().BeNull();
        (await EmailOfAsync(db, solo)).Should().Be("solo@exemplo.com");
        (await EmailOfAsync(db, inAnotherCondominium)).Should().Be("ana@exemplo.com");
        (await EmailOfAsync(db, noEmailOne)).Should().BeNull();
        (await EmailOfAsync(db, noEmailTwo)).Should().BeNull();
        await InsertingTheSameEmailAgain(db, condominium);
    }

    [Fact]
    public async Task The_migration_runs_on_a_database_without_any_resident()
    {
        await using CondominiumDbContext db = await CreateDatabaseAtAsync(BeforeTheIndex);

        Func<Task> migrate = () => db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        await migrate.Should().NotThrowAsync();
        (await IndexDefinitionAsync(db)).Should().Contain("UNIQUE").And.Contain("lower((email)::text)").And.Contain("email IS NOT NULL");
    }

    private static async Task InsertingTheSameEmailAgain(CondominiumDbContext db, Guid condominium)
    {
        Func<Task> act = () => InsertPersonAsync(db, condominium, "Bia@Exemplo.com", createdAt: "2026-05-01");

        (await act.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ix_person_condominium_id_email");
    }

    private async Task<CondominiumDbContext> CreateDatabaseAtAsync(string migration)
    {
        string database = $"urban_migration_{Guid.NewGuid():N}";
        await using (NpgsqlConnection admin = new(postgres.ConnectionString))
        {
            await admin.OpenAsync(Ct);
            await using NpgsqlCommand create = new($"create database {database}", admin);
            await create.ExecuteNonQueryAsync(Ct);
        }

        string connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = database, Pooling = false }.ConnectionString;
        CondominiumDbContext db = new(CondominiumPersistence.CreateOptions(connectionString), postgres.Protector, new FixedTenant(null));
        await db.GetService<IMigrator>().MigrateAsync(migration, Ct);

        return db;
    }

    private static async Task<Guid> InsertCondominiumAsync(CondominiumDbContext db)
    {
        Guid id = Guid.CreateVersion7();
        string cnpj = Random.Shared.NextInt64(10_000_000_000_000, 99_999_999_999_999).ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into condominium.condominium (id, name, cnpj, address_street, address_number, address_neighborhood, address_city, address_state, address_postal_code, created_by, created_at)
            values ({id}, 'Antigo', {cnpj}, 'Rua A', '1', 'Centro', 'Blumenau', 'SC', '89010000', {Guid.CreateVersion7()}, now())
            """, Ct);

        return id;
    }

    private static async Task<Guid> InsertPersonAsync(CondominiumDbContext db, Guid condominium, string? email, string createdAt, Guid? userId = null)
    {
        Guid id = Guid.CreateVersion7();
        string hash = Guid.NewGuid().ToString("N");
        DateTimeOffset created = DateTimeOffset.Parse($"{createdAt}T12:00:00+00:00");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into condominium.person (id, condominium_id, user_id, full_name, cpf_encrypted, cpf_hash, birth_date, phone, email, created_at)
            values ({id}, {condominium}, {userId}, 'Pessoa', 'x', {hash}, '1990-05-20', '47998765432', {email}, {created})
            """, Ct);

        return id;
    }

    private static async Task<string?> EmailOfAsync(CondominiumDbContext db, Guid id) =>
        await db.Database.SqlQuery<string?>($"""select email as "Value" from condominium.person where id = {id}""").SingleAsync(Ct);

    private static async Task<string> IndexDefinitionAsync(CondominiumDbContext db) =>
        await db.Database
            .SqlQuery<string>($"""select indexdef as "Value" from pg_indexes where schemaname = 'condominium' and indexname = 'ix_person_condominium_id_email'""")
            .SingleAsync(Ct);
}
