using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence.Configurations;

internal sealed class ChangeLogConfiguration : IEntityTypeConfiguration<ChangeLogRecord>
{
    public void Configure(EntityTypeBuilder<ChangeLogRecord> builder)
    {
        builder.ToTable("change_log");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasOne<CondominiumEntity>().WithMany().HasForeignKey(c => c.CondominiumId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.ActorUserId).IsRequired();
        builder.Property(c => c.Kind).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Fields).IsRequired();
        builder.Property(c => c.ChangedAt);

        builder.HasIndex(c => new { c.CondominiumId, c.ChangedAt });
    }
}
