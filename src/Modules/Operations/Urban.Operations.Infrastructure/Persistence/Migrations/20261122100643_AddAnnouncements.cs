using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Operations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "announcement",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement", x => x.id);
                    table.CheckConstraint("ck_announcement_audience", "(scope = 'Condominium' and block is null and unit_id is null) or (scope = 'Block' and block is not null and unit_id is null) or (scope = 'Unit' and block is null and unit_id is not null)");
                });

            migrationBuilder.CreateTable(
                name: "announcement_read",
                schema: "operations",
                columns: table => new
                {
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement_read", x => new { x.announcement_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_announcement_read_announcement_announcement_id",
                        column: x => x.announcement_id,
                        principalSchema: "operations",
                        principalTable: "announcement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_condominium_id_created_at",
                schema: "operations",
                table: "announcement",
                columns: new[] { "condominium_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_read_user_id",
                schema: "operations",
                table: "announcement_read",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_read",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "announcement",
                schema: "operations");
        }
    }
}
