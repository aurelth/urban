using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Tokens;

/// <summary>Emite o token de acesso: JWT assinado (HS256) com identidade, papel, condomínio (tenant) e um jti para revogação.</summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials;
    private readonly JwtOptions _options;

    public JwtTokenIssuer(JwtOptions options)
    {
        _options = options;
        _credentials = new SigningCredentials(new SymmetricSecurityKey(options.GetSigningKeyBytes()), SecurityAlgorithms.HmacSha256);
    }

    public AccessToken IssueAccessToken(User user, DateTimeOffset now)
    {
        DateTimeOffset expiresAt = now + _options.AccessTokenLifetime;
        Dictionary<string, object> claims = new()
        {
            ["sub"] = user.Id.ToString(),
            ["jti"] = Guid.CreateVersion7().ToString(),
            ["role"] = user.Role.ToString(),
        };

        if (user.CondominiumId is { } condominiumId)
        {
            claims["condominium_id"] = condominiumId.ToString();
        }

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
