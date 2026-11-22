using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Urban.Operations.Domain.Announcements;
using Urban.Operations.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Operations.Persistence;

/// <summary>O que o banco garante sozinho, mesmo que um código futuro erre: o alcance coerente e as leituras presas ao comunicado.</summary>
[Collection(OperationsPostgresCollection.Name)]
public class OperationsModelTests(OperationsPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly OperationsStoreKit _kit = new(postgres);

    [Theory]
    [InlineData("Condominium", "Bloco A", false)]
    [InlineData("Condominium", null, true)]
    [InlineData("Block", null, false)]
    [InlineData("Block", "Bloco A", true)]
    [InlineData("Unit", null, false)]
    [InlineData("Unit", "Bloco A", true)]
    public async Task The_database_refuses_an_audience_that_does_not_match_the_scope(string scope, string? block, bool hasUnit)
    {
        await using OperationsDbContext db = _kit.Context();

        Func<Task> insert = async () => await db.Database.ExecuteSqlRawAsync(
            "insert into operations.announcement (id, condominium_id, title, body, category, scope, block, unit_id, created_by, created_at) "
            + "values (gen_random_uuid(), gen_random_uuid(), 't', 'b', 'General', {0}, {1}, {2}, gen_random_uuid(), now())",
            [
                new NpgsqlParameter { Value = scope, NpgsqlDbType = NpgsqlDbType.Varchar },
                new NpgsqlParameter { Value = (object?)block ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Varchar },
                new NpgsqlParameter { Value = hasUnit ? Guid.CreateVersion7() : DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
            ],
            Ct);

        (await insert.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_announcement_audience");
    }

    [Fact]
    public async Task The_database_accepts_the_three_valid_audiences()
    {
        await _kit.StoreAsync();
        await _kit.StoreAsync(OperationsStoreKit.Block("Bloco A"));
        await _kit.StoreAsync(OperationsStoreKit.Unit(Guid.CreateVersion7()));

        (await _kit.Announcements().ListAsync(1, 20, Ct)).Total.Should().Be(3);
    }

    [Fact]
    public async Task The_enums_are_stored_as_text_not_as_numbers()
    {
        Announcement stored = await _kit.StoreAsync(OperationsStoreKit.Block("Bloco A"), category: AnnouncementCategory.Important);
        await using OperationsDbContext db = _kit.Context();

        List<string> values = await db.Database
            .SqlQuery<string>($"select category || '/' || scope as \"Value\" from operations.announcement where id = {stored.Id}")
            .ToListAsync(Ct);

        values.Should().Equal("Important/Block");
    }

    [Fact]
    public async Task A_read_cannot_exist_without_its_announcement()
    {
        await using OperationsDbContext db = _kit.Context();

        Func<Task> insert = async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"insert into operations.announcement_read (announcement_id, user_id, condominium_id, read_at) values ({Guid.CreateVersion7()}, {Guid.CreateVersion7()}, {_kit.CondominiumId}, now())", Ct);

        (await insert.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("fk_announcement_read_announcement_announcement_id");
    }

    [Fact]
    public async Task The_announcement_version_is_the_row_version_of_the_database()
    {
        Announcement stored = await _kit.StoreAsync();
        await using OperationsDbContext db = _kit.Context(_kit.CondominiumId);

        uint version = (await db.Announcements.SingleAsync(item => item.Id == stored.Id, Ct)) is { } found ? db.Entry(found).Property<uint>("xmin").CurrentValue : 0;

        version.Should().BeGreaterThan(0u);
    }
}
