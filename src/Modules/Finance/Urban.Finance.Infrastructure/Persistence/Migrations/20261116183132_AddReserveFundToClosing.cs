using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReserveFundToClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "reserve_fund_base",
                schema: "finance",
                table: "month_closing",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reserve_fund_percent",
                schema: "finance",
                table: "month_closing",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reserve_fund_base",
                schema: "finance",
                table: "month_closing");

            migrationBuilder.DropColumn(
                name: "reserve_fund_percent",
                schema: "finance",
                table: "month_closing");
        }
    }
}
