using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class IdentityModelTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public void The_migrations_describe_the_current_model()
    {
        using IdentityDbContext db = postgres.CreateContext();

        db.Database.HasPendingModelChanges().Should().BeFalse("toda mudança no modelo precisa de uma migration");
    }

    [Fact]
    public async Task The_tables_live_in_the_identity_schema()
    {
        await using IdentityDbContext db = postgres.CreateContext();

        List<string> tables = await db.Database
            .SqlQuery<string>($"""select table_name as "Value" from information_schema.tables where table_schema = 'identity'""")
            .ToListAsync(Ct);

        tables.Should().Contain(["user", "refresh_token", "__ef_migrations_history"]);
    }
}
