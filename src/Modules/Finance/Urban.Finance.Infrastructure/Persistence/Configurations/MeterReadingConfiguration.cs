using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Consumption;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    /// <summary>Uma leitura por unidade, mês e serviço: informar de novo corrige a leitura em vez de somar outra.</summary>
    public const string UnitMonthServiceIndex = "ix_meter_reading_condominium_unit_month_service";

    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("meter_reading");

        builder.HasKey(reading => reading.Id);
        builder.Property(reading => reading.Id).ValueGeneratedNever();

        builder.Property(reading => reading.CondominiumId).IsRequired();
        builder.Property(reading => reading.UnitId).IsRequired();
        builder.Property(reading => reading.Month).HasConversion<YearMonthConverter>().HasMaxLength(YearMonthConverter.Length).IsRequired();
        builder.Property(reading => reading.Service).HasConversion<string>().HasMaxLength(10).IsRequired();

        // Leituras em m³ com 3 casas (a planilha guarda litros).
        builder.Property(reading => reading.Previous).HasPrecision(10, 3);
        builder.Property(reading => reading.Current).HasPrecision(10, 3);
        builder.Property(reading => reading.CreatedAt);

        builder.HasIndex(reading => new { reading.CondominiumId, reading.UnitId, reading.Month, reading.Service }).IsUnique().HasDatabaseName(UnitMonthServiceIndex);
    }
}
