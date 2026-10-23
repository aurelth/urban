using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication.Logout;

/// <param name="UserId">Quem está saindo (vem do token de acesso).</param>
/// <param name="AccessTokenId">O jti do token de acesso atual.</param>
/// <param name="AccessTokenExpiresAt">Quando o token de acesso atual expira naturalmente.</param>
/// <param name="RefreshToken">O valor do cookie de renovação, quando existir.</param>
public sealed record LogoutCommand(Guid UserId, string AccessTokenId, DateTimeOffset AccessTokenExpiresAt, string? RefreshToken)
    : IRequest<Result>;
