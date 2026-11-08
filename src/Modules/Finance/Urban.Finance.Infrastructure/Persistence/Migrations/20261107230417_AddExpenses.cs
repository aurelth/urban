using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "expense_group",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_group", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    supplier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    end_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    installment_count = table.Column<int>(type: "integer", nullable: true),
                    current_installment = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_expense_group_group_id",
                        column: x => x.group_id,
                        principalSchema: "finance",
                        principalTable: "expense_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_amount_change",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_amount_change", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_amount_change_expense_expense_id",
                        column: x => x.expense_id,
                        principalSchema: "finance",
                        principalTable: "expense",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expense_entry",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    description = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    label = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_entry_expense_expense_id",
                        column: x => x.expense_id,
                        principalSchema: "finance",
                        principalTable: "expense",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expense_entry_expense_group_group_id",
                        column: x => x.group_id,
                        principalSchema: "finance",
                        principalTable: "expense_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_installment",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_installment", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_installment_expense_expense_id",
                        column: x => x.expense_id,
                        principalSchema: "finance",
                        principalTable: "expense",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_condominium_id",
                schema: "finance",
                table: "expense",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_group_id",
                schema: "finance",
                table: "expense",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_amount_change_month",
                schema: "finance",
                table: "expense_amount_change",
                columns: new[] { "expense_id", "from_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_entry_condominium_id_month",
                schema: "finance",
                table: "expense_entry",
                columns: new[] { "condominium_id", "month" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_entry_expense_month",
                schema: "finance",
                table: "expense_entry",
                columns: new[] { "expense_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_entry_group_id",
                schema: "finance",
                table: "expense_entry",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_group_condominium_id",
                schema: "finance",
                table: "expense_group",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_installment_number",
                schema: "finance",
                table: "expense_installment",
                columns: new[] { "expense_id", "number" },
                unique: true);

            // Índice único com expressão, que o EF não modela: o nome do grupo não repete no condomínio, sem diferenciar maiúsculas.
            migrationBuilder.Sql(
                "create unique index ix_expense_group_name on finance.expense_group (condominium_id, lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_amount_change",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense_entry",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense_installment",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense_group",
                schema: "finance");
        }
    }
}
