using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int EnumNameMaxLength = 30;
    private const int StatusReasonMaxLength = 500;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasMaxLength(Email.MaxLength)
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(EnumNameMaxLength).IsRequired();
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(EnumNameMaxLength).IsRequired();

        builder.Property(u => u.CondominiumId);
        builder.HasIndex(u => u.CondominiumId);

        builder.Property(u => u.CreatedAt);
        builder.Property(u => u.StatusChangedAt);
        builder.Property(u => u.StatusChangedBy);
        builder.Property(u => u.StatusReason).HasMaxLength(StatusReasonMaxLength);
        builder.Property(u => u.DeletedAt);
    }
}
