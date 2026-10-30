using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Além do mapeamento comum dos links, guarda o endereço novo pedido: o link só vale para ele.</summary>
internal sealed class EmailChangeTokenConfiguration() : EmailLinkTokenConfiguration<EmailChangeToken>("email_change_token")
{
    protected override void ConfigureToken(EntityTypeBuilder<EmailChangeToken> builder)
    {
        builder.Property(t => t.NewEmail)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasMaxLength(Email.MaxLength)
            .IsRequired();
    }
}
