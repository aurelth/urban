using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Application.Abstractions;

public static class EmailChangesExtensions
{
    /// <summary>Diz se a conta já recebeu um link há pouco (veja <c>EmailLinkToken.ResendInterval</c>).</summary>
    public static async Task<bool> HasRecentLinkAsync(
        this IEmailChanges changes,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        EmailChangeToken? latest = await changes.FindLatestOfUserAsync(userId, cancellationToken);

        return latest is not null && latest.WasIssuedRecently(now);
    }
}
