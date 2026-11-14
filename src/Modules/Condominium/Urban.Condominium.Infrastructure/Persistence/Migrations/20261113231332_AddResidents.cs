using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Condominium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "condominium",
                table: "person",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "address_street",
                schema: "condominium",
                table: "person",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "address_state",
                schema: "condominium",
                table: "person",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<string>(
                name: "address_postal_code",
                schema: "condominium",
                table: "person",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<string>(
                name: "address_number",
                schema: "condominium",
                table: "person",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "address_neighborhood",
                schema: "condominium",
                table: "person",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "address_city",
                schema: "condominium",
                table: "person",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "condominium",
                table: "person",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emergency_contact_name",
                schema: "condominium",
                table: "person",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emergency_contact_phone",
                schema: "condominium",
                table: "person",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emergency_contact_relationship",
                schema: "condominium",
                table: "person",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profession",
                schema: "condominium",
                table: "person",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "occupancy",
                schema: "condominium",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_occupancy", x => x.id);
                    table.ForeignKey(
                        name: "fk_occupancy_condominium_condominium_id",
                        column: x => x.condominium_id,
                        principalSchema: "condominium",
                        principalTable: "condominium",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_occupancy_person_person_id",
                        column: x => x.person_id,
                        principalSchema: "condominium",
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_occupancy_unit_unit_id",
                        column: x => x.unit_id,
                        principalSchema: "condominium",
                        principalTable: "unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_occupancy_condominium_id_unit_id",
                schema: "condominium",
                table: "occupancy",
                columns: new[] { "condominium_id", "unit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_occupancy_person_id",
                schema: "condominium",
                table: "occupancy",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_occupancy_person_unit_active",
                schema: "condominium",
                table: "occupancy",
                columns: new[] { "person_id", "unit_id" },
                unique: true,
                filter: "end_date is null");

            migrationBuilder.CreateIndex(
                name: "ix_occupancy_unit_id",
                schema: "condominium",
                table: "occupancy",
                column: "unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occupancy",
                schema: "condominium");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "condominium",
                table: "person");

            migrationBuilder.DropColumn(
                name: "emergency_contact_name",
                schema: "condominium",
                table: "person");

            migrationBuilder.DropColumn(
                name: "emergency_contact_phone",
                schema: "condominium",
                table: "person");

            migrationBuilder.DropColumn(
                name: "emergency_contact_relationship",
                schema: "condominium",
                table: "person");

            migrationBuilder.DropColumn(
                name: "profession",
                schema: "condominium",
                table: "person");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "condominium",
                table: "person",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_street",
                schema: "condominium",
                table: "person",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_state",
                schema: "condominium",
                table: "person",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_postal_code",
                schema: "condominium",
                table: "person",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_number",
                schema: "condominium",
                table: "person",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_neighborhood",
                schema: "condominium",
                table: "person",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_city",
                schema: "condominium",
                table: "person",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);
        }
    }
}
