using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Condominium.Domain.Documents;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence.Configurations;

internal sealed class CondominiumConfiguration : IEntityTypeConfiguration<CondominiumEntity>
{
    public void Configure(EntityTypeBuilder<CondominiumEntity> builder)
    {
        builder.ToTable("condominium");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();

        // Um CNPJ, um condomínio, na plataforma inteira.
        builder.Property(c => c.Cnpj)
            .HasConversion(cnpj => cnpj.Value, value => Cnpj.Create(value).Value)
            .HasMaxLength(14)
            .IsRequired();
        builder.HasIndex(c => c.Cnpj).IsUnique();

        builder.ComplexProperty(c => c.Address, address => address.MapAddress());

        // Um síndico, um condomínio: também fecha a corrida de dois cadastros simultâneos da mesma conta.
        builder.Property(c => c.CreatedBy).IsRequired();
        builder.HasIndex(c => c.CreatedBy).IsUnique();

        builder.Property(c => c.CreatedAt);

        // Versão da linha para a edição: a coluna de sistema xmin muda a cada UPDATE. Fica como propriedade-sombra para o Domain não conhecer o EF.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
