using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    /// <summary>O hash do BCrypt tem 60 caracteres; a folga cobre uma troca futura de algoritmo.</summary>
    private const int PasswordHashMaxLength = 100;

    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credential");

        builder.HasKey(c => c.UserId);
        builder.Property(c => c.UserId).ValueGeneratedNever();

        builder.HasOne<User>().WithOne().HasForeignKey<UserCredential>(c => c.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.PasswordHash).HasMaxLength(PasswordHashMaxLength).IsRequired();
        builder.Property(c => c.FailedAttempts);
        builder.Property(c => c.LockoutEnd);

        // Token de concorrência: a coluna de sistema xmin do PostgreSQL muda a cada UPDATE da linha.
        // Fica como propriedade-sombra para o Domain não conhecer o EF.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}