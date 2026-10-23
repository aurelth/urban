using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthTokens>>;
