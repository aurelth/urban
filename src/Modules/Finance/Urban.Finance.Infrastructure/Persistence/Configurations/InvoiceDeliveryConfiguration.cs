using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Invoices;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceDeliveryConfiguration : IEntityTypeConfiguration<InvoiceDelivery>
{
    public const string ChargeIndex = "ix_invoice_delivery_unit_charge_id";

    public void Configure(EntityTypeBuilder<InvoiceDelivery> builder)
    {
        builder.ToTable("invoice_delivery");

        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).ValueGeneratedNever();

        builder.Property(delivery => delivery.CondominiumId).IsRequired();
        builder.Property(delivery => delivery.ChargeId).HasColumnName("unit_charge_id").IsRequired();
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(delivery => delivery.Attempts);
        builder.Property(delivery => delivery.RequestedBy);
        builder.Property(delivery => delivery.RequestedAt);
        builder.Property(delivery => delivery.LastAttemptAt);
        builder.Property(delivery => delivery.SentAt);
        builder.Property(delivery => delivery.FailureReason).HasMaxLength(InvoiceDelivery.FailureReasonMaxLength);

        // Cascade: reabrir o mês apaga o fechamento e as cobranças, e o envio de uma cobrança que não existe mais some com elas.
        builder.HasOne<UnitCharge>().WithMany().HasForeignKey(delivery => delivery.ChargeId).OnDelete(DeleteBehavior.Cascade);

        // Uma linha por cobrança: o envio e cada reenvio a reaproveitam. É também o que barra dois "enviar" ao mesmo tempo.
        builder.HasIndex(delivery => delivery.ChargeId).IsUnique().HasDatabaseName(ChargeIndex);
        builder.HasIndex(delivery => delivery.CondominiumId);
    }
}
