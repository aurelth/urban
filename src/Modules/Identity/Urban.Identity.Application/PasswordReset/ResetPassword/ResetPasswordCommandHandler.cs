using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.PasswordReset.ResetPassword;

/// <summary>
/// Quem abre o link escolhe a nova senha. A senha é validada antes de o link ser consultado: senha fraca não gasta o link.
/// Depois de gravar, encerra todas as sessões da pessoa (token de renovação e de acesso): quem tinha a senha antiga, ou o
/// motivo do pedido de redefinição, não continua dentro da conta. A mesma gravação desbloqueia a conta.
/// </summary>
public sealed class ResetPasswordCommandHandler(
    IPasswordResets resets,
    IUserAccounts accounts,
    ISecretGenerator secrets,
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokens,
    ISessionRevocations sessions,
    IClock clock) : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        Result policy = PasswordPolicy.Validate(request.Password);
        if (policy.IsFailure)
        {
            return policy;
        }

        PasswordResetToken? token = await resets.FindByHashAsync(secrets.Hash(request.Token), cancellationToken);
        if (token is null)
        {
            return Result.Failure(PasswordResetTokenErrors.Invalid);
        }

        DateTimeOffset now = clock.UtcNow;
        Result consumed = token.Consume(now);
        if (consumed.IsFailure)
        {
            return consumed;
        }

        User? user = await accounts.FindByIdAsync(token.UserId, cancellationToken);
        UserCredential? credential = user is { Status: AccountStatus.Active }
            ? await resets.FindCredentialOfUserAsync(user.Id, cancellationToken)
            : null;
        if (user is null || credential is null)
        {
            return Result.Failure(PasswordResetTokenErrors.Invalid);
        }

        credential.ChangePassword(passwordHasher.Hash(request.Password));
        if (!await resets.ResetAsync(token, credential, cancellationToken))
        {
            return Result.Failure(PasswordResetTokenErrors.Invalid);
        }

        await refreshTokens.RevokeAllOfUserAsync(user.Id, now, cancellationToken);
        await sessions.RevokeAllOfUserAsync(user.Id, now, cancellationToken);

        return Result.Success();
    }
}