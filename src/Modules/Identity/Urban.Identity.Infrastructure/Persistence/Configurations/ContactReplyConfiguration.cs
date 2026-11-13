using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

internal sealed class ContactReplyConfiguration : IEntityTypeConfiguration<ContactReply>
{
    public void Configure(EntityTypeBuilder<ContactReply> builder)
    {
        builder.ToTable("contact_reply");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Body).HasMaxLength(ContactReply.MaxBodyLength).IsRequired();
        builder.Property(r => r.SentAt);
        builder.Property(r => r.Attachments)
            .HasConversion(new ContactReplyAttachmentsConverter(), ContactReplyAttachmentsConverter.Comparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne<ContactMessage>().WithMany().HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.MessageId);
    }
}
