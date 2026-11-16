using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_instructions",
                schema: "finance",
                columns: table => new
                {
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    account_agency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    account_bank = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    account_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    account_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    pix_key = table.Column<string>(type: "character varying(77)", maxLength: 77, nullable: true),
                    pix_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_instructions", x => x.condominium_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_instructions",
                schema: "finance");
        }
    }
}
