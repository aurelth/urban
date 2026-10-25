using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Authentication.Logout;

/// <summary>Sair é sempre idempotente e bem-sucedido: não revela se o token existia nem de quem era.</summary>
public sealed class LogoutCommandHandler(
    ISecretGenerator refreshTokenGenerator,
    IRefreshTokenRepository refreshTokens,
    ISessionRevocations sessions,
    IClock clock) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await sessions.RevokeAccessTokenAsync(request.AccessTokenId, request.AccessTokenExpiresAt, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            string hash = refreshTokenGenerator.Hash(request.RefreshToken);
            RefreshToken? token = await refreshTokens.FindByHashAsync(hash, cancellationToken);

            // Só revoga o token da própria pessoa: conhecer o valor de outro usuário não dá poder sobre ele.
            if (token is not null && token.UserId == request.UserId)
            {
                token.Revoke(clock.UtcNow);
                await refreshTokens.UpdateAsync(token, cancellationToken);
            }
        }

        return Result.Success();
    }
}
