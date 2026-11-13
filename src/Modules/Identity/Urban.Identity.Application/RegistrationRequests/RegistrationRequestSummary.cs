using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.RegistrationRequests;

public sealed record RegistrationRequestSummary(Guid Id, string Email, string Name, string CondominiumName, DateTimeOffset RequestedAt)
{
    public static RegistrationRequestSummary From(User user) => new(
        user.Id,
        user.Email.Value,
        user.DisplayName?.Value ?? user.Email.Value,
        user.RequestedCondominiumName?.Value ?? string.Empty,
        user.CreatedAt);
}
