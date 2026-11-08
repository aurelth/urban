using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meter_reading",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    service = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    previous = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    current = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_reading", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meter_reading_condominium_unit_month_service",
                schema: "finance",
                table: "meter_reading",
                columns: new[] { "condominium_id", "unit_id", "month", "service" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_reading",
                schema: "finance");
        }
    }
}
