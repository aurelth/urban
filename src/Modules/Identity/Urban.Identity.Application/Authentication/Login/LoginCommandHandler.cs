using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Authentication.Login;

public sealed class LoginCommandHandler(
    IUserAccounts accounts,
    ITokenIssuer tokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IRefreshTokenRepository refreshTokens,
    IClock clock) : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return Result.Failure<LoginResult>(LoginErrors.InvalidCredentials);
        }

        Result<User> credentials = await accounts.CheckCredentialsAsync(email.Value, request.Password, cancellationToken);
        if (credentials.IsFailure)
        {
            return Result.Failure<LoginResult>(credentials.Error);
        }

        // O estado da conta só é revelado depois da senha correta (CheckCanSignIn).
        User user = credentials.Value;
        Result canSignIn = user.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return Result.Failure<LoginResult>(canSignIn.Error);
        }

        DateTimeOffset now = clock.UtcNow;
        AccessToken accessToken = tokenIssuer.IssueAccessToken(user, now);
        RefreshSecret secret = refreshTokenGenerator.Generate();
        RefreshToken refreshToken = RefreshToken.Issue(user.Id, secret.Hash, now);
        await refreshTokens.AddAsync(refreshToken, cancellationToken);

        return Result.Success(new LoginResult(accessToken.Value, accessToken.ExpiresAt, secret.Value, refreshToken.ExpiresAt));
    }
}
