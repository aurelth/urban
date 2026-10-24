using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using Urban.Identity.Application.Abstractions;

namespace Urban.Api.Http;

public sealed record AccessTokenSession(Guid UserId, string TokenId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt)
{
    public static bool TryRead(ClaimsPrincipal principal, [NotNullWhen(true)] out AccessTokenSession? session)
    {
        session = null;
        string? tokenId = principal.FindFirstValue(AccessTokenClaims.TokenId);

        if (!Guid.TryParse(principal.FindFirstValue(AccessTokenClaims.Subject), out Guid userId)
            || string.IsNullOrEmpty(tokenId)
            || !TryReadSeconds(principal, AccessTokenClaims.IssuedAt, out DateTimeOffset issuedAt)
            || !TryReadSeconds(principal, AccessTokenClaims.ExpiresAt, out DateTimeOffset expiresAt))
        {
            return false;
        }

        session = new AccessTokenSession(userId, tokenId, issuedAt, expiresAt);
        return true;
    }

    private static bool TryReadSeconds(ClaimsPrincipal principal, string claimType, out DateTimeOffset value)
    {
        bool parsed = long.TryParse(principal.FindFirstValue(claimType), NumberStyles.None, CultureInfo.InvariantCulture, out long seconds);
        value = parsed ? DateTimeOffset.FromUnixTimeSeconds(seconds) : default;
        return parsed;
    }
}