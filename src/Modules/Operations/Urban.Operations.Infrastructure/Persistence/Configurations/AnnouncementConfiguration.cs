using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Operations.Domain.Announcements;

namespace Urban.Operations.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcement", table => table.HasCheckConstraint(
            "ck_announcement_audience",
            "(scope = 'Condominium' and block is null and unit_id is null)"
            + " or (scope = 'Block' and block is not null and unit_id is null)"
            + " or (scope = 'Unit' and block is null and unit_id is not null)"));

        builder.HasKey(announcement => announcement.Id);
        builder.Property(announcement => announcement.Id).ValueGeneratedNever();

        builder.Property(announcement => announcement.CondominiumId).IsRequired();
        builder.Property(announcement => announcement.Title).HasMaxLength(Announcement.TitleMaxLength).IsRequired();
        builder.Property(announcement => announcement.Body).HasMaxLength(Announcement.BodyMaxLength).IsRequired();
        builder.Property(announcement => announcement.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(announcement => announcement.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(announcement => announcement.Block).HasMaxLength(AnnouncementAudience.BlockMaxLength);
        builder.Property(announcement => announcement.UnitId);
        builder.Property(announcement => announcement.CreatedBy).IsRequired();
        builder.Property(announcement => announcement.CreatedAt);
        builder.Property(announcement => announcement.UpdatedAt);
        builder.Property(announcement => announcement.NotifyByEmail);
        builder.Ignore(announcement => announcement.Audience);

        builder.HasIndex(announcement => new { announcement.CondominiumId, announcement.CreatedAt });

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
