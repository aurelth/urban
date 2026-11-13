using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.RegistrationRequests.NotifyRegistrationRequest;

public sealed record NotifyRegistrationRequestCommand(Guid UserId) : IRequest<Result>;
