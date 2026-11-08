using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "month_closing",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    basis_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_month_closing", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unit_charge",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    closing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(14,8)", precision: 14, scale: 8, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unit_charge", x => x.id);
                    table.ForeignKey(
                        name: "fk_unit_charge_month_closing_closing_id",
                        column: x => x.closing_id,
                        principalSchema: "finance",
                        principalTable: "month_closing",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_month_closing_condominium_month",
                schema: "finance",
                table: "month_closing",
                columns: new[] { "condominium_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unit_charge_closing_id_unit_id",
                schema: "finance",
                table: "unit_charge",
                columns: new[] { "closing_id", "unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unit_charge_condominium_id_unit_id",
                schema: "finance",
                table: "unit_charge",
                columns: new[] { "condominium_id", "unit_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unit_charge",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "month_closing",
                schema: "finance");
        }
    }
}
