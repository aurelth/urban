using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Condominium.Persistence;

[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumModelTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public void The_migrations_describe_the_current_model()
    {
        using CondominiumDbContext db = postgres.CreateContext();

        db.Database.HasPendingModelChanges().Should().BeFalse("toda mudança no modelo precisa de uma migration");
    }

    [Fact]
    public async Task The_tables_live_in_the_condominium_schema()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        List<string> tables = await db.Database
            .SqlQuery<string>($"""select table_name as "Value" from information_schema.tables where table_schema = 'condominium'""")
            .ToListAsync(Ct);

        tables.Should().Contain(["condominium", "person", "__ef_migrations_history"]);
    }

    [Theory]
    [InlineData("condominium", "cnpj", "character varying", 14)]
    [InlineData("condominium", "name", "character varying", 120)]
    [InlineData("condominium", "address_street", "character varying", 120)]
    [InlineData("condominium", "address_number", "character varying", 10)]
    [InlineData("condominium", "address_complement", "character varying", 60)]
    [InlineData("condominium", "address_neighborhood", "character varying", 80)]
    [InlineData("condominium", "address_city", "character varying", 80)]
    [InlineData("condominium", "address_state", "character varying", 2)]
    [InlineData("condominium", "address_postal_code", "character varying", 8)]
    [InlineData("person", "full_name", "character varying", 120)]
    [InlineData("person", "phone", "character varying", 11)]
    [InlineData("person", "cpf_hash", "character varying", 64)]
    [InlineData("person", "address_postal_code", "character varying", 8)]
    public async Task Columns_have_the_type_and_length_of_the_domain_rules(string table, string column, string type, int length)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        ColumnInfo info = await db.Database.SqlQuery<ColumnInfo>($"""
            select data_type as data_type, character_maximum_length as length from information_schema.columns
            where table_schema = 'condominium' and table_name = {table} and column_name = {column}
            """).SingleAsync(Ct);

        info.DataType.Should().Be(type);
        info.Length.Should().Be(length);
    }

    [Fact]
    public async Task Dates_use_the_proper_postgres_types()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        List<string> types = await db.Database.SqlQuery<string>($"""
            select data_type as "Value" from information_schema.columns
            where table_schema = 'condominium' and
                  ((table_name = 'person' and column_name in ('birth_date', 'created_at')) or (table_name = 'condominium' and column_name = 'created_at'))
            order by table_name, column_name
            """).ToListAsync(Ct);

        types.Should().Equal("timestamp with time zone", "date", "timestamp with time zone");
    }

    private sealed record ColumnInfo(string DataType, int Length);
}
