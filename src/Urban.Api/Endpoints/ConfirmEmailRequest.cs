namespace Urban.Api.Endpoints;

public sealed record ConfirmEmailRequest(string? Token, string? Password);