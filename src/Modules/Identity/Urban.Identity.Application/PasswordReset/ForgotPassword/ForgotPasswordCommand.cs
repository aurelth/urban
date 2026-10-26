using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.PasswordReset.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;