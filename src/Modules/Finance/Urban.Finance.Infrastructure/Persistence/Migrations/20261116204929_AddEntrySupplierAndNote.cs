using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntrySupplierAndNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "note",
                schema: "finance",
                table: "expense_entry",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier",
                schema: "finance",
                table: "expense_entry",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            // Os lançamentos que já existem nasceram com o fornecedor que a despesa tinha, então ele é copiado agora.
            migrationBuilder.Sql(
                """
                UPDATE finance.expense_entry AS entry
                SET supplier = expense.supplier
                FROM finance.expense AS expense
                WHERE expense.id = entry.expense_id AND expense.supplier IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "note",
                schema: "finance",
                table: "expense_entry");

            migrationBuilder.DropColumn(
                name: "supplier",
                schema: "finance",
                table: "expense_entry");
        }
    }
}
