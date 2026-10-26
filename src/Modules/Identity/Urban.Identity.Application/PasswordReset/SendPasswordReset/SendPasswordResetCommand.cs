using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.PasswordReset.SendPasswordReset;

public sealed record SendPasswordResetCommand(Guid UserId) : IRequest<Result>;