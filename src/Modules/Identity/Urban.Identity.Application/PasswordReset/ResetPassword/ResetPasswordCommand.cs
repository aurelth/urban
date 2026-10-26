using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.PasswordReset.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string Password) : IRequest<Result>;