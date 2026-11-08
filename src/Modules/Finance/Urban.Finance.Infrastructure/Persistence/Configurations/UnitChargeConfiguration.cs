using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Closing;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class UnitChargeConfiguration : IEntityTypeConfiguration<UnitCharge>
{
    public void Configure(EntityTypeBuilder<UnitCharge> builder)
    {
        builder.ToTable("unit_charge");

        builder.HasKey(charge => charge.Id);
        builder.Property(charge => charge.Id).ValueGeneratedNever();

        // Sem chave estrangeira para a unidade: ela mora no módulo Condomínio. Bloco e número são cópias do fechamento.
        builder.Property(charge => charge.CondominiumId).IsRequired();
        builder.Property(charge => charge.UnitId).IsRequired();
        builder.Property(charge => charge.Position);
        builder.Property(charge => charge.Block).HasMaxLength(30);
        builder.Property(charge => charge.Number).HasMaxLength(20).IsRequired();

        // Fração ideal (até 8 casas), área (2 casas) ou 1: cabe nos três.
        builder.Property(charge => charge.Weight).HasPrecision(14, 8);
        builder.Property(charge => charge.Amount).HasPrecision(9, 2);

        builder.HasMany(charge => charge.Lines).WithOne().HasForeignKey(line => line.ChargeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(charge => charge.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(charge => new { charge.ClosingId, charge.UnitId }).IsUnique();
        builder.HasIndex(charge => new { charge.CondominiumId, charge.UnitId });
    }
}
