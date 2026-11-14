using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    /// <summary>
    /// Uma placa por condomínio. A placa é gravada normalizada (maiúsculas, sem hífen), então um índice comum basta para
    /// "ABC-1234" e "abc1234" valerem como a mesma.
    /// </summary>
    public const string PlateIndex = "ix_vehicle_plate";

    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicle");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.HasOne<CondominiumEntity>().WithMany().HasForeignKey(v => v.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CondominiumUnit>().WithMany().HasForeignKey(v => v.UnitId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(v => v.Plate).HasMaxLength(LicensePlate.Length).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(Vehicle.ModelMaxLength).IsRequired();
        builder.Property(v => v.Color).HasMaxLength(Vehicle.ColorMaxLength).IsRequired();
        builder.Property(v => v.CreatedAt);

        builder.HasIndex(v => new { v.CondominiumId, v.Plate }).IsUnique().HasDatabaseName(PlateIndex);

        // Os veículos de cada unidade.
        builder.HasIndex(v => new { v.CondominiumId, v.UnitId });

        // Versão da linha para a edição (xmin), como propriedade-sombra para o Domain não conhecer o EF.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
