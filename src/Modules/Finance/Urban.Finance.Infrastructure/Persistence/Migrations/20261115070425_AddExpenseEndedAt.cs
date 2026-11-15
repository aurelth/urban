using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseEndedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ended_at",
                schema: "finance",
                table: "expense",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ended_at",
                schema: "finance",
                table: "expense");
        }
    }
}
