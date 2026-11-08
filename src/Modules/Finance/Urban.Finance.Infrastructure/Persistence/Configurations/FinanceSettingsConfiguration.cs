using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Settings;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class FinanceSettingsConfiguration : IEntityTypeConfiguration<FinanceSettings>
{
    /// <summary>A chave é o próprio condomínio: uma linha só por condomínio, e duas requisições criando ao mesmo tempo esbarram nela.</summary>
    public const string PrimaryKey = "pk_finance_settings";

    public void Configure(EntityTypeBuilder<FinanceSettings> builder)
    {
        builder.ToTable("finance_settings");

        builder.HasKey(settings => settings.CondominiumId).HasName(PrimaryKey);
        builder.Property(settings => settings.CondominiumId).ValueGeneratedNever();
        builder.Property(settings => settings.ReserveFundPercent).HasPrecision(5, 2);
        builder.Property(settings => settings.UpdatedAt);
    }
}
