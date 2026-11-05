using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Condominium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:public.natural_order", "und-u-kn-true,und-u-kn-true,icu,False");

            migrationBuilder.AddColumn<string>(
                name: "apportionment_method",
                schema: "condominium",
                table: "condominium",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "IdealFraction");

            migrationBuilder.AddColumn<Guid>(
                name: "subject_id",
                schema: "condominium",
                table: "change_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "unit",
                schema: "condominium",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ideal_fraction = table.Column<decimal>(type: "numeric(9,8)", precision: 9, scale: 8, nullable: true),
                    area = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unit", x => x.id);
                    table.ForeignKey(
                        name: "fk_unit_condominium_condominium_id",
                        column: x => x.condominium_id,
                        principalSchema: "condominium",
                        principalTable: "condominium",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_unit_condominium_id_deactivated_at",
                schema: "condominium",
                table: "unit",
                columns: new[] { "condominium_id", "deactivated_at" });

            // Índice único com expressões, que o EF não modela: bloco vazio é "sem bloco" e as duas partes não diferenciam
            // maiúsculas. Vale para unidades ativas e desativadas, então o identificador de uma desativada continua reservado.
            migrationBuilder.Sql(
                "create unique index ix_unit_identifier on condominium.unit (condominium_id, lower(coalesce(block, '')), lower(number));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unit",
                schema: "condominium");

            migrationBuilder.DropColumn(
                name: "apportionment_method",
                schema: "condominium",
                table: "condominium");

            migrationBuilder.DropColumn(
                name: "subject_id",
                schema: "condominium",
                table: "change_log");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:CollationDefinition:public.natural_order", "und-u-kn-true,und-u-kn-true,icu,False");
        }
    }
}
