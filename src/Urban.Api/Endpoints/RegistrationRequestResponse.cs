using Urban.Identity.Application.RegistrationRequests;

namespace Urban.Api.Endpoints;

public sealed record RegistrationRequestResponse(Guid Id, string Email, string Name, string CondominiumName, DateTimeOffset RequestedAt)
{
    public static RegistrationRequestResponse From(RegistrationRequestSummary request) =>
        new(request.Id, request.Email, request.Name, request.CondominiumName, request.RequestedAt);
}
