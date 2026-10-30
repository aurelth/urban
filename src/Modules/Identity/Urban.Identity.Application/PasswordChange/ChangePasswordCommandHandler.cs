using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.PasswordChange;

/// <summary>
/// Troca de senha com a pessoa logada. As regras que não dependem do banco vêm primeiro (senha nova fraca ou igual à atual
/// não gasta tentativa da senha atual); depois a senha atual é conferida pelo mesmo caminho do login, então o erro conta
/// para o bloqueio. Gravada a nova senha, todas as sessões da pessoa terminam (a do aparelho que trocou também: ela entra
/// de novo) e o e-mail da conta recebe um aviso.
/// </summary>
public sealed class ChangePasswordCommandHandler(
    IUserAccounts accounts,
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokens,
    ISessionRevocations sessions,
    ISecurityNoticeDispatcher notices,
    IClock clock) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        Result policy = PasswordPolicy.Validate(request.NewPassword);
        if (policy.IsFailure)
        {
            return policy;
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return Result.Failure(UserErrors.PasswordUnchanged);
        }

        User? user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(SessionErrors.Unauthenticated);
        }

        Result canSignIn = user.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return canSignIn;
        }

        Result<User> confirmed = await accounts.CheckCredentialsAsync(user.Email, request.CurrentPassword ?? string.Empty, cancellationToken);
        if (confirmed.IsFailure)
        {
            return Result.Failure(confirmed.Error == LoginErrors.LockedOut ? LoginErrors.LockedOut : UserErrors.PasswordIncorrect);
        }

        UserCredential? credential = await accounts.FindCredentialAsync(user.Id, cancellationToken);
        if (credential is null)
        {
            return Result.Failure(SessionErrors.Unauthenticated);
        }

        credential.ChangePassword(passwordHasher.Hash(request.NewPassword!));
        await accounts.SaveCredentialAsync(credential, cancellationToken);

        DateTimeOffset now = clock.UtcNow;
        await refreshTokens.RevokeAllOfUserAsync(user.Id, now, cancellationToken);
        await sessions.RevokeAllOfUserAsync(user.Id, now, cancellationToken);
        await notices.EnqueueAsync(SecurityNotice.PasswordChanged, user.Email, null, cancellationToken);

        return Result.Success();
    }
}
