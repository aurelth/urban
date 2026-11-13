using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.RegistrationRequests.RejectRegistration;

public sealed record RejectRegistrationCommand(Guid ActorId, Guid UserId) : IRequest<Result>;
