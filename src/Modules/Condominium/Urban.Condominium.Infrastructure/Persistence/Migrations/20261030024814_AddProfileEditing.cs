using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Condominium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "condominium",
                table: "person",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "condominium",
                table: "condominium",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "change_log",
                schema: "condominium",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fields = table.Column<string[]>(type: "text[]", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_change_log_condominium_condominium_id",
                        column: x => x.condominium_id,
                        principalSchema: "condominium",
                        principalTable: "condominium",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_change_log_condominium_id_changed_at",
                schema: "condominium",
                table: "change_log",
                columns: new[] { "condominium_id", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_log",
                schema: "condominium");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "condominium",
                table: "person");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "condominium",
                table: "condominium");
        }
    }
}
