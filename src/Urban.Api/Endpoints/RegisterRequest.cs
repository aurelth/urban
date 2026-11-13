namespace Urban.Api.Endpoints;

public sealed record RegisterRequest(string? Email, string? Name, string? CondominiumName);