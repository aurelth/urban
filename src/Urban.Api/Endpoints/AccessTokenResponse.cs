namespace Urban.Api.Endpoints;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt);