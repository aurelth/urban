using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Condominium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle",
                schema: "condominium",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicle", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicle_condominium_condominium_id",
                        column: x => x.condominium_id,
                        principalSchema: "condominium",
                        principalTable: "condominium",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vehicle_unit_unit_id",
                        column: x => x.unit_id,
                        principalSchema: "condominium",
                        principalTable: "unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_condominium_id_unit_id",
                schema: "condominium",
                table: "vehicle",
                columns: new[] { "condominium_id", "unit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_plate",
                schema: "condominium",
                table: "vehicle",
                columns: new[] { "condominium_id", "plate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_unit_id",
                schema: "condominium",
                table: "vehicle",
                column: "unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle",
                schema: "condominium");
        }
    }
}
