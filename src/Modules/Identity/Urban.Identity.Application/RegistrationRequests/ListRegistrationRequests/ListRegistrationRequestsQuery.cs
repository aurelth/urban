using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.RegistrationRequests.ListRegistrationRequests;

public sealed record ListRegistrationRequestsQuery(Guid ActorId) : IRequest<Result<IReadOnlyList<RegistrationRequestSummary>>>;
