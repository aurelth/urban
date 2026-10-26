namespace Urban.Api.Endpoints;

public sealed record ResetPasswordRequest(string? Token, string? Password);