using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Condominium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitParkingSpots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "parking_spots",
                schema: "condominium",
                table: "unit",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "parking_spots",
                schema: "condominium",
                table: "unit");
        }
    }
}
