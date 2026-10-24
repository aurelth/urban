using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Urban.IntegrationTests.Api;

internal static class ForgedTokens
{
    private static readonly JsonWebTokenHandler Handler = new();

    public static string Create(
        string signingKey,
        string algorithm = SecurityAlgorithms.HmacSha256,
        string issuer = "urban",
        string audience = "urban",
        bool expired = false)
    {
        DateTime now = DateTime.UtcNow;

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = Claims(),
            IssuedAt = expired ? now.AddMinutes(-20) : now,
            NotBefore = expired ? now.AddMinutes(-20) : now,
            Expires = expired ? now.AddMinutes(-1) : now.AddMinutes(5),
            SigningCredentials = Credentials(signingKey, algorithm),
        });
    }

    public static string WithoutExpiration(string signingKey)
    {
        Dictionary<string, object> claims = Claims();
        claims["iss"] = "urban";
        claims["aud"] = "urban";
        claims["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return Handler.CreateToken(JsonSerializer.Serialize(claims), Credentials(signingKey, SecurityAlgorithms.HmacSha256));
    }

    public static string Unsigned() =>
        Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "urban",
            Audience = "urban",
            Claims = Claims(),
            Expires = DateTime.UtcNow.AddMinutes(5),
        });

    public static string NewSigningKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static SigningCredentials Credentials(string signingKey, string algorithm) =>
        new(new SymmetricSecurityKey(Convert.FromBase64String(signingKey)), algorithm);

    private static Dictionary<string, object> Claims() => new()
    {
        ["sub"] = Guid.CreateVersion7().ToString(),
        ["jti"] = Guid.CreateVersion7().ToString(),
        ["role"] = "Resident",
    };
}