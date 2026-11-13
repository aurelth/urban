using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "requested_condominium_name",
                schema: "identity",
                table: "user",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "requested_condominium_name",
                schema: "identity",
                table: "user");
        }
    }
}
