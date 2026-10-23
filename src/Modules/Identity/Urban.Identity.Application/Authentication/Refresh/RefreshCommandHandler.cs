using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Authentication.Refresh;

public sealed class RefreshCommandHandler(
    IUserAccounts accounts,
    ITokenIssuer tokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IRefreshTokenRepository refreshTokens,
    ISessionRevocations sessions,
    IClock clock) : IRequestHandler<RefreshCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        string hash = refreshTokenGenerator.Hash(request.RefreshToken);
        RefreshToken? current = await refreshTokens.FindByHashAsync(hash, cancellationToken);
        if (current is null)
        {
            return Result.Failure<AuthTokens>(RefreshErrors.InvalidSession);
        }

        // Antes de girar o token: conta suspensa, desativada ou apagada não renova sessão, e nada é alterado.
        User? user = await accounts.FindByIdAsync(current.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthTokens>(RefreshErrors.InvalidSession);
        }

        Result canSignIn = user.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return Result.Failure<AuthTokens>(canSignIn.Error);
        }

        DateTimeOffset now = clock.UtcNow;
        RefreshSecret secret = refreshTokenGenerator.Generate();
        Result<RefreshToken> rotation = current.Rotate(secret.Hash, now);
        if (rotation.IsFailure)
        {
            if (rotation.Error == RefreshTokenErrors.Reused)
            {
                // Token já usado reaparecendo = possível roubo: derruba todas as sessões da pessoa.
                await refreshTokens.RevokeAllOfUserAsync(user.Id, now, cancellationToken);
                await sessions.RevokeAllOfUserAsync(user.Id, now, cancellationToken);
            }

            return Result.Failure<AuthTokens>(rotation.Error);
        }

        await refreshTokens.ReplaceAsync(current, rotation.Value, cancellationToken);
        AccessToken accessToken = tokenIssuer.IssueAccessToken(user, now);
        return Result.Success(AuthTokens.From(accessToken, secret, rotation.Value));
    }
}
