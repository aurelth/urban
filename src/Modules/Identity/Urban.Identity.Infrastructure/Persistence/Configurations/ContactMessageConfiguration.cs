using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("contact_message");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Subject).HasMaxLength(ContactMessage.MaxSubjectLength).IsRequired();
        builder.Property(m => m.Body).HasMaxLength(ContactMessage.MaxBodyLength).IsRequired();
        builder.Property(m => m.CondominiumId);
        builder.Property(m => m.CreatedAt);
        builder.Property(m => m.ReadAt);

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.CreatedAt);
        builder.HasIndex(m => m.SenderId);
    }
}
