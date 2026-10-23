using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication.Refresh;

public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<AuthTokens>>;
