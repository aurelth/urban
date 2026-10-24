namespace Urban.Api.Endpoints;

public sealed record LoginRequest(string? Email, string? Password);