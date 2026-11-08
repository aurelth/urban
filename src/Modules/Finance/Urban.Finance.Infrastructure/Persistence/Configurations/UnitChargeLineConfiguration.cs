using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Closing;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class UnitChargeLineConfiguration : IEntityTypeConfiguration<UnitChargeLine>
{
    public void Configure(EntityTypeBuilder<UnitChargeLine> builder)
    {
        builder.ToTable("unit_charge_line");

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();

        builder.Property(line => line.CondominiumId).IsRequired();
        builder.Property(line => line.Position);
        builder.Property(line => line.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(line => line.Label).HasMaxLength(60).IsRequired();
        builder.Property(line => line.Amount).HasPrecision(9, 2);

        // Consumo e leituras em m³ com 3 casas (a planilha guarda litros).
        builder.Property(line => line.Quantity).HasPrecision(10, 3);
        builder.Property(line => line.PreviousReading).HasPrecision(10, 3);
        builder.Property(line => line.CurrentReading).HasPrecision(10, 3);

        builder.HasIndex(line => new { line.ChargeId, line.Position }).IsUnique();
    }
}
