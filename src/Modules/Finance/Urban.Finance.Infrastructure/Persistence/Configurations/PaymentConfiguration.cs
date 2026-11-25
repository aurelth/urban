using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Payments;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payment");

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();

        builder.Property(payment => payment.CondominiumId).IsRequired();
        builder.Property(payment => payment.ChargeId).HasColumnName("unit_charge_id").IsRequired();
        builder.Property(payment => payment.Amount).HasPrecision(14, 2);
        builder.Property(payment => payment.ReceivedOn);
        builder.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.Note).HasMaxLength(Payment.NoteMaxLength);
        builder.Property(payment => payment.RegisteredBy);
        builder.Property(payment => payment.RegisteredAt);
        builder.Property(payment => payment.ReceiptNumber);
        builder.Property(payment => payment.ReversedAt);
        builder.Property(payment => payment.ReversedBy);
        builder.Property(payment => payment.ReversalReason).HasMaxLength(Payment.ReasonMaxLength);
        builder.Ignore(payment => payment.IsReversed);

        // Restrict: cobrança com pagamento não se apaga, nem por cascata do fechamento.
        builder.HasOne<UnitCharge>().WithMany().HasForeignKey(payment => payment.ChargeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(payment => new { payment.CondominiumId, payment.ChargeId });

        // Um número de recibo por pagamento e por condomínio: é a segunda barreira, depois do contador, contra um número repetido.
        builder.HasIndex(payment => new { payment.CondominiumId, payment.ReceiptNumber }).IsUnique();
    }
}
