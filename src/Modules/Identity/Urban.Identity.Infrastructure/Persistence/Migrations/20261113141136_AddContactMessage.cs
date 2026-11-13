using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContactMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact_message",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_contact_message_user_sender_id",
                        column: x => x.sender_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contact_message_created_at",
                schema: "identity",
                table: "contact_message",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_contact_message_sender_id",
                schema: "identity",
                table: "contact_message",
                column: "sender_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_message",
                schema: "identity");
        }
    }
}
