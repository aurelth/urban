using Microsoft.IdentityModel.Tokens;
using Urban.Identity.Application.Abstractions;

namespace Urban.Identity.Infrastructure.Tokens;

public static class AccessTokenValidation
{
    public static TokenValidationParameters CreateParameters(JwtOptions options) => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(options.GetSigningKeyBytes()),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = AccessTokenClaims.Subject,
        RoleClaimType = AccessTokenClaims.Role,
    };
}