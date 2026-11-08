using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Finance.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceModelTests(FinancePostgresFixture postgres)
{
    [Fact]
    public void TheMigrationsMatchTheModel()
    {
        using FinanceDbContext db = postgres.CreateContext();

        db.Database.HasPendingModelChanges().Should().BeFalse("toda mudança no modelo precisa de uma migration");
    }

    [Fact]
    public async Task TheTablesLiveInTheFinanceSchema()
    {
        await using FinanceDbContext db = postgres.CreateContext();

        List<string> tables = await db.Database
            .SqlQuery<string>($"select table_name as \"Value\" from information_schema.tables where table_schema = 'finance' order by table_name")
            .ToListAsync();

        tables.Should().Contain(["__ef_migrations_history", "expense", "expense_amount_change", "expense_entry", "expense_group", "expense_installment", "finance_settings", "consumption_tariff", "meter_reading", "month_closing", "unit_charge", "unit_charge_line"]);
    }
}
