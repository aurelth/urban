namespace Urban.Identity.Application.Authentication.Login;

/// <summary>O endpoint devolve o token de acesso no corpo e grava o de renovação em cookie HttpOnly.</summary>
public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
