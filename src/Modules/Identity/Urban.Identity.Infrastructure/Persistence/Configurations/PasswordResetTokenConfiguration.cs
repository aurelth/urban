using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration() : EmailLinkTokenConfiguration<PasswordResetToken>("password_reset_token");