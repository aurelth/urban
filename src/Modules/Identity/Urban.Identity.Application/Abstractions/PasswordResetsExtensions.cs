using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Application.Abstractions;

public static class PasswordResetsExtensions
{
    /// <summary>Diz se a conta já recebeu um link há pouco (veja <c>EmailLinkToken.ResendInterval</c>).</summary>
    public static async Task<bool> HasRecentLinkAsync(
        this IPasswordResets resets,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PasswordResetToken? latest = await resets.FindLatestOfUserAsync(userId, cancellationToken);

        return latest is not null && latest.WasIssuedRecently(now);
    }
}