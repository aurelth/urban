using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "receipt_number",
                schema: "finance",
                table: "payment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "receipt_counter",
                schema: "finance",
                columns: table => new
                {
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_counter", x => x.condominium_id);
                });

            // Os pagamentos que já existem ganham o número pela ordem em que foram registrados, condomínio a condomínio, e o contador
            // de cada condomínio começa no último número dado. Isso vem antes do índice único, porque todos nascem com 0.
            migrationBuilder.Sql(
                """
                update finance.payment as payment
                set receipt_number = numbered.position
                from (
                    select id, row_number() over (partition by condominium_id order by registered_at, id) as position
                    from finance.payment
                ) as numbered
                where numbered.id = payment.id;
                """);

            migrationBuilder.Sql(
                """
                insert into finance.receipt_counter (condominium_id, last_number)
                select condominium_id, max(receipt_number)
                from finance.payment
                group by condominium_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_payment_condominium_id_receipt_number",
                schema: "finance",
                table: "payment",
                columns: new[] { "condominium_id", "receipt_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_counter",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "ix_payment_condominium_id_receipt_number",
                schema: "finance",
                table: "payment");

            migrationBuilder.DropColumn(
                name: "receipt_number",
                schema: "finance",
                table: "payment");
        }
    }
}
