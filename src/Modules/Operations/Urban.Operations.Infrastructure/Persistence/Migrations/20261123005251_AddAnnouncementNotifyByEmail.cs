using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Operations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementNotifyByEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "notify_by_email",
                schema: "operations",
                table: "announcement",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "notify_by_email",
                schema: "operations",
                table: "announcement");
        }
    }
}
