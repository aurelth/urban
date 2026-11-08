using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumptionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reserve_fund_base",
                schema: "finance",
                table: "expense_group",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "consumption_tariff",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    water = table.Column<string>(type: "jsonb", nullable: false),
                    sewage = table.Column<string>(type: "jsonb", nullable: true),
                    sewage_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    gas = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumption_tariff", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finance_settings",
                schema: "finance",
                columns: table => new
                {
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reserve_fund_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_finance_settings", x => x.condominium_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consumption_tariff_condominium_valid_from",
                schema: "finance",
                table: "consumption_tariff",
                columns: new[] { "condominium_id", "valid_from" },
                unique: true);

            // O grupo padrão sempre foi a base natural do fundo de reserva (na planilha, 15% das despesas ordinárias).
            migrationBuilder.Sql("UPDATE finance.expense_group SET reserve_fund_base = true WHERE lower(name) = lower('Despesas ordinárias');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumption_tariff",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "finance_settings",
                schema: "finance");

            migrationBuilder.DropColumn(
                name: "reserve_fund_base",
                schema: "finance",
                table: "expense_group");
        }
    }
}
