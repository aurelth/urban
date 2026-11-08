using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDueDateAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Escrita à mão sobre o que o EF gerou: despesas e lançamentos que já existem recebem valores razoáveis
            // (vencimento no dia 10, situação pendente) e os valores padrão das colunas saem em seguida, porque o
            // modelo exige que cada despesa nova informe o dia.
            migrationBuilder.AddColumn<int>(
                name: "due_day",
                schema: "finance",
                table: "expense",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                schema: "finance",
                table: "expense_entry",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "finance",
                table: "expense_entry",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            // O mês é guardado como "AAAA-MM"; o dia 10 existe em todos os meses, então não precisa ajustar o fim do mês.
            migrationBuilder.Sql("UPDATE finance.expense_entry SET due_date = (month || '-10')::date;");

            migrationBuilder.AlterColumn<int>(
                name: "due_day",
                schema: "finance",
                table: "expense",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 10);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "due_date",
                schema: "finance",
                table: "expense_entry",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldDefaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "finance",
                table: "expense_entry",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "due_date",
                schema: "finance",
                table: "expense_entry");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "finance",
                table: "expense_entry");

            migrationBuilder.DropColumn(
                name: "due_day",
                schema: "finance",
                table: "expense");
        }
    }
}
