using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitChargeLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unit_charge_line",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    previous_reading = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    current_reading = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unit_charge_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_unit_charge_line_unit_charge_charge_id",
                        column: x => x.charge_id,
                        principalSchema: "finance",
                        principalTable: "unit_charge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_unit_charge_line_charge_id_position",
                schema: "finance",
                table: "unit_charge_line",
                columns: new[] { "charge_id", "position" },
                unique: true);

            // Meses já fechados não tinham linhas: cada cobrança ganha uma linha única com o valor que já tinha.
            migrationBuilder.Sql(
                """
                INSERT INTO finance.unit_charge_line (id, charge_id, condominium_id, position, kind, label, amount)
                SELECT gen_random_uuid(), id, condominium_id, 0, 'Expenses', 'Despesas', amount
                FROM finance.unit_charge;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unit_charge_line",
                schema: "finance");
        }
    }
}
