using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Operations.Domain.Announcements;

namespace Urban.Operations.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> builder)
    {
        builder.ToTable("announcement_read");

        // Uma leitura por comunicado e conta: marcar de novo não cria outra linha.
        builder.HasKey(read => new { read.AnnouncementId, read.UserId });

        builder.Property(read => read.CondominiumId).IsRequired();
        builder.Property(read => read.ReadAt);

        // Cascade: apagar o comunicado leva junto as leituras dele.
        builder.HasOne<Announcement>().WithMany().HasForeignKey(read => read.AnnouncementId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(read => read.UserId);
    }
}
