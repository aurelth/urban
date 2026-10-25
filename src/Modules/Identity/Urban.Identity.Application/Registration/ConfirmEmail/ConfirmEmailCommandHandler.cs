using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Registration.ConfirmEmail;

/// <summary>Quem abre o link escolhe a senha e a conta fica ativa. A senha é validada antes de o link ser consultado: senha fraca não gasta o link.</summary>
public sealed class ConfirmEmailCommandHandler(
    IEmailVerifications verifications,
    IUserAccounts accounts,
    ISecretGenerator secrets,
    IPasswordHasher passwordHasher,
    IClock clock) : IRequestHandler<ConfirmEmailCommand, Result>
{
    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        Result policy = PasswordPolicy.Validate(request.Password);
        if (policy.IsFailure)
        {
            return policy;
        }

        EmailVerificationToken? token = await verifications.FindByHashAsync(secrets.Hash(request.Token), cancellationToken);
        if (token is null)
        {
            return Result.Failure(EmailVerificationTokenErrors.Invalid);
        }

        DateTimeOffset now = clock.UtcNow;
        Result consumed = token.Consume(now);
        if (consumed.IsFailure)
        {
            return consumed;
        }

        User? user = await accounts.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.ConfirmEmail(now).IsFailure)
        {
            return Result.Failure(EmailVerificationTokenErrors.Invalid);
        }

        UserCredential credential = UserCredential.Create(user.Id, passwordHasher.Hash(request.Password));
        bool confirmed = await verifications.ConfirmAsync(token, user, credential, cancellationToken);

        return confirmed ? Result.Success() : Result.Failure(EmailVerificationTokenErrors.Invalid);
    }
}