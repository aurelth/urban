using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.RegistrationRequests.ApproveRegistration;

public sealed record ApproveRegistrationCommand(Guid ActorId, Guid UserId) : IRequest<Result>;
