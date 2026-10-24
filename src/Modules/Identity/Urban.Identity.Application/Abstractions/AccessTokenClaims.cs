namespace Urban.Identity.Application.Abstractions;

public static class AccessTokenClaims
{
    public const string Subject = "sub";

    public const string TokenId = "jti";

    public const string IssuedAt = "iat";

    public const string ExpiresAt = "exp";

    public const string Role = "role";

    public const string CondominiumId = "condominium_id";
}