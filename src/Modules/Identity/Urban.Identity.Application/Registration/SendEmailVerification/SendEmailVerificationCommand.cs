using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Registration.SendEmailVerification;

public sealed record SendEmailVerificationCommand(Guid UserId) : IRequest<Result>;