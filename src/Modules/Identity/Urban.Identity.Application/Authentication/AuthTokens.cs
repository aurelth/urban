namespace Urban.Identity.Application.Authentication;

/// <summary>O endpoint devolve o token de acesso no corpo e grava o de renovação em cookie HttpOnly.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
