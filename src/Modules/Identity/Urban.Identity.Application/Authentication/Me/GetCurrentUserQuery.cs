using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication.Me;

/// <param name="UserId">Quem está logado (vem do token de acesso).</param>
public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<CurrentUser>>;