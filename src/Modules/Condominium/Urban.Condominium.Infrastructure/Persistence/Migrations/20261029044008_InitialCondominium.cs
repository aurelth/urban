using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Condominium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCondominium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "condominium");

            migrationBuilder.CreateTable(
                name: "condominium",
                schema: "condominium",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    address_city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    address_complement = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    address_neighborhood = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    address_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    address_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address_street = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condominium", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person",
                schema: "condominium",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cpf_encrypted = table.Column<string>(type: "text", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    phone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cpf_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    address_city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    address_complement = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    address_neighborhood = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    address_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    address_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address_street = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person", x => x.id);
                    table.ForeignKey(
                        name: "fk_person_condominium_condominium_id",
                        column: x => x.condominium_id,
                        principalSchema: "condominium",
                        principalTable: "condominium",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_condominium_cnpj",
                schema: "condominium",
                table: "condominium",
                column: "cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_condominium_created_by",
                schema: "condominium",
                table: "condominium",
                column: "created_by",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_condominium_id_cpf_hash",
                schema: "condominium",
                table: "person",
                columns: new[] { "condominium_id", "cpf_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_user_id",
                schema: "condominium",
                table: "person",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person",
                schema: "condominium");

            migrationBuilder.DropTable(
                name: "condominium",
                schema: "condominium");
        }
    }
}
