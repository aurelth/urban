using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Infrastructure.Persistence.Configurations;

internal sealed class EmailVerificationTokenConfiguration() : EmailLinkTokenConfiguration<EmailVerificationToken>("email_verification_token");