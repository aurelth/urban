using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.EmailChange.SendEmailChange;

public sealed record SendEmailChangeCommand(Guid UserId, string NewEmail) : IRequest<Result>;
