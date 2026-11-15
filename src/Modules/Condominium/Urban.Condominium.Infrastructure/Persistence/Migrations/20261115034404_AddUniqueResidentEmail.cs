using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Condominium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueResidentEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os testes com o sistema já deixaram o mesmo e-mail em mais de um morador. O índice não nasceria com eles, então, para cada
            // (condomínio, e-mail sem diferenciar maiúsculas), fica o e-mail de quem já tem conta (a conta usa aquele endereço), depois o do
            // cadastro mais antigo, e os outros ficam sem e-mail (o campo é opcional: o resto do cadastro não muda).
            migrationBuilder.Sql(
                """
                with ranked as (
                    select id,
                           row_number() over (
                               partition by condominium_id, lower(email)
                               order by (user_id is null), created_at, id) as position
                    from condominium.person
                    where email is not null)
                update condominium.person set email = null
                where id in (select id from ranked where position > 1);
                """);

            // Índice único com expressão, que o EF não modela: o e-mail não diferencia maiúsculas e só vale para quem tem e-mail.
            migrationBuilder.Sql(
                "create unique index ix_person_condominium_id_email on condominium.person (condominium_id, lower(email)) where email is not null;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop index condominium.ix_person_condominium_id_email;");
        }
    }
}
