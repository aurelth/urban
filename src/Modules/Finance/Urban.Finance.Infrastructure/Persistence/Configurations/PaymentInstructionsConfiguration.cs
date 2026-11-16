using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Payments;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class PaymentInstructionsConfiguration : IEntityTypeConfiguration<PaymentInstructions>
{
    /// <summary>A chave é o próprio condomínio: uma linha só por condomínio, e duas requisições criando ao mesmo tempo esbarram nela.</summary>
    public const string PrimaryKey = "pk_payment_instructions";

    public void Configure(EntityTypeBuilder<PaymentInstructions> builder)
    {
        builder.ToTable("payment_instructions");

        builder.HasKey(instructions => instructions.CondominiumId).HasName(PrimaryKey);
        builder.Property(instructions => instructions.CondominiumId).ValueGeneratedNever();

        // Cada parte é opcional: quando falta, todas as colunas dela ficam nulas (colunas pix_* e account_*).
        builder.ComplexProperty(instructions => instructions.Pix, pix =>
        {
            pix.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
            pix.Property(p => p.Key).HasMaxLength(PixKey.MaxLength).IsRequired();
        });

        builder.ComplexProperty(instructions => instructions.Account, account =>
        {
            account.Property(a => a.Bank).HasMaxLength(BankAccount.BankMaxLength).IsRequired();
            account.Property(a => a.Agency).HasMaxLength(8).IsRequired();
            account.Property(a => a.Number).HasMaxLength(15).IsRequired();
            account.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        builder.Property(instructions => instructions.UpdatedAt);
    }
}
