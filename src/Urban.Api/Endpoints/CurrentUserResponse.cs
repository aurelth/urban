namespace Urban.Api.Endpoints;

public sealed record CurrentUserResponse(Guid Id, string Email, string Role, Guid? CondominiumId, string? DisplayName);