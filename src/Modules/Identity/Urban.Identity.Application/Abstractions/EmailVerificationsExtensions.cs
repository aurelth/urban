using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Application.Abstractions;

public static class EmailVerificationsExtensions
{
    /// <summary>Diz se a conta já recebeu um link há pouco (veja <c>EmailVerificationToken.ResendInterval</c>).</summary>
    public static async Task<bool> HasRecentLinkAsync(
        this IEmailVerifications verifications,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        EmailVerificationToken? latest = await verifications.FindLatestOfUserAsync(userId, cancellationToken);

        return latest is not null && latest.WasIssuedRecently(now);
    }
}