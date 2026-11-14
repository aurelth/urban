using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence.Configurations;

internal sealed class OccupancyConfiguration : IEntityTypeConfiguration<Occupancy>
{
    /// <summary>Só um vínculo em andamento por pessoa e unidade; os que já terminaram (histórico) podem se repetir.</summary>
    public const string ActivePerPersonAndUnitIndex = "ix_occupancy_person_unit_active";

    public void Configure(EntityTypeBuilder<Occupancy> builder)
    {
        builder.ToTable("occupancy");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.HasOne<CondominiumEntity>().WithMany().HasForeignKey(o => o.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CondominiumUnit>().WithMany().HasForeignKey(o => o.UnitId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(o => o.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.StartDate).HasColumnType("date");
        builder.Property(o => o.EndDate).HasColumnType("date");
        builder.Property(o => o.CreatedAt);

        builder.Ignore(o => o.IsActive);

        builder.HasIndex(o => new { o.PersonId, o.UnitId })
            .IsUnique()
            .HasFilter("end_date is null")
            .HasDatabaseName(ActivePerPersonAndUnitIndex);

        // Quem mora em cada unidade, e a lista de vínculos de cada pessoa.
        builder.HasIndex(o => new { o.CondominiumId, o.UnitId });
        builder.HasIndex(o => o.PersonId);
    }
}
