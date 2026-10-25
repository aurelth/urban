using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

internal sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    /// <summary>Tamanho do hash SHA-256 em hexadecimal.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("email_verification_token");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TokenHash).HasMaxLength(TokenHashLength).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.CreatedAt);
        builder.Property(t => t.ExpiresAt);
        builder.Property(t => t.UsedAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // O último link da conta decide se um novo e-mail pode ser enviado.
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });

        // Duas requisições com o mesmo link: só a primeira grava. A coluna de sistema xmin muda a cada UPDATE da linha.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}