using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContactReply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact_reply",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    attachments = table.Column<string>(type: "jsonb", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_reply", x => x.id);
                    table.ForeignKey(
                        name: "fk_contact_reply_contact_message_message_id",
                        column: x => x.message_id,
                        principalSchema: "identity",
                        principalTable: "contact_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contact_reply_user_author_id",
                        column: x => x.author_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contact_reply_author_id",
                schema: "identity",
                table: "contact_reply",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_contact_reply_message_id",
                schema: "identity",
                table: "contact_reply",
                column: "message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_reply",
                schema: "identity");
        }
    }
}
