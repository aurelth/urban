using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Consumption;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class ConsumptionTariffConfiguration : IEntityTypeConfiguration<ConsumptionTariff>
{
    /// <summary>Uma tarifa por condomínio e mês de início: repetir o mês troca a tarifa em vez de somar outra.</summary>
    public const string CondominiumValidFromIndex = "ix_consumption_tariff_condominium_valid_from";

    public void Configure(EntityTypeBuilder<ConsumptionTariff> builder)
    {
        builder.ToTable("consumption_tariff");

        builder.HasKey(tariff => tariff.Id);
        builder.Property(tariff => tariff.Id).ValueGeneratedNever();

        builder.Property(tariff => tariff.CondominiumId).IsRequired();
        builder.Property(tariff => tariff.ValidFrom).HasConversion<YearMonthConverter>().HasMaxLength(YearMonthConverter.Length).IsRequired();
        builder.Property(tariff => tariff.Water).HasConversion<TariffScaleConverter>().HasColumnType("jsonb").IsRequired();
        builder.Property(tariff => tariff.Sewage).HasConversion<TariffScaleConverter>().HasColumnType("jsonb");
        builder.Property(tariff => tariff.SewagePercent).HasPrecision(5, 2);
        builder.Property(tariff => tariff.Gas).HasConversion<GasTariffConverter>().HasColumnType("jsonb");
        builder.Property(tariff => tariff.CreatedAt);

        builder.HasIndex(tariff => new { tariff.CondominiumId, tariff.ValidFrom }).IsUnique().HasDatabaseName(CondominiumValidFromIndex);
    }
}
