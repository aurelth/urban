using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class ReceiptCounterConfiguration : IEntityTypeConfiguration<ReceiptCounter>
{
    public void Configure(EntityTypeBuilder<ReceiptCounter> builder)
    {
        builder.ToTable("receipt_counter");

        builder.HasKey(counter => counter.CondominiumId);
        builder.Property(counter => counter.CondominiumId).ValueGeneratedNever();
        builder.Property(counter => counter.LastNumber);
    }
}
