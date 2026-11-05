using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Condominium.Domain.Units;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence.Configurations;

internal sealed class UnitConfiguration : IEntityTypeConfiguration<CondominiumUnit>
{
    /// <summary>
    /// Nome do índice único do identificador (condomínio, bloco sem diferenciar maiúsculas, número sem diferenciar
    /// maiúsculas). Ele usa expressões, que o EF não modela, então é criado por SQL na migration.
    /// </summary>
    public const string IdentifierIndex = "ix_unit_identifier";

    public void Configure(EntityTypeBuilder<CondominiumUnit> builder)
    {
        builder.ToTable("unit");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.HasOne<CondominiumEntity>().WithMany().HasForeignKey(u => u.CondominiumId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.Block).HasMaxLength(CondominiumUnit.BlockMaxLength);
        builder.Property(u => u.Number).HasMaxLength(CondominiumUnit.NumberMaxLength).IsRequired();
        builder.Property(u => u.Type).HasConversion<string>().HasMaxLength(20).IsRequired();

        // numeric(9,8): de 0 a 1 com 8 casas. numeric(7,2): até 99.999,99 m².
        builder.Property(u => u.IdealFraction).HasPrecision(9, 8);
        builder.Property(u => u.Area).HasPrecision(7, 2);

        builder.Property(u => u.CreatedAt);
        builder.Property(u => u.DeactivatedAt);

        builder.Ignore(u => u.Identity);

        // Lista por condomínio, ativas primeiro nos filtros mais comuns.
        builder.HasIndex(u => new { u.CondominiumId, u.DeactivatedAt });

        // Versão da linha para a edição (xmin), como propriedade-sombra para o Domain não conhecer o EF.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
