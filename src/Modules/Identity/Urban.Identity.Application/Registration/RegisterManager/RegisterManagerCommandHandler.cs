using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Registration.RegisterManager;

/// <summary>
/// Cadastro da síndica: só o e-mail. A resposta é a mesma para e-mail novo, conta pendente ou conta já ativa, então
/// ninguém descobre quais e-mails têm conta. A senha é escolhida depois, ao confirmar o e-mail.
/// </summary>
public sealed class RegisterManagerCommandHandler(
    IUserAccounts accounts,
    IEmailVerifications verifications,
    ISecretGenerator secrets,
    IEmailVerificationNotifier notifier,
    IClock clock) : IRequestHandler<RegisterManagerCommand, Result>
{
    public async Task<Result> Handle(RegisterManagerCommand request, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return Result.Failure(email.Error);
        }

        User? existing = await accounts.FindByEmailAsync(email.Value, cancellationToken);
        DateTimeOffset now = clock.UtcNow;

        if (existing is null)
        {
            await RegisterNewAsync(email.Value, now, cancellationToken);
        }
        else if (existing.Status == AccountStatus.PendingVerification)
        {
            await ResendAsync(existing, email.Value, now, cancellationToken);
        }

        return Result.Success();
    }

    private async Task RegisterNewAsync(Email email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        User user = User.Register(email, UserRole.Manager, null, now).Value;
        GeneratedSecret secret = secrets.Generate();
        EmailVerificationToken token = EmailVerificationToken.Issue(user.Id, secret.Hash, now);

        if (await verifications.TryRegisterAsync(user, token, cancellationToken))
        {
            await notifier.SendAsync(email, secret.Value, cancellationToken);
        }
    }

    private async Task ResendAsync(User user, Email email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        EmailVerificationToken? latest = await verifications.FindLatestOfUserAsync(user.Id, cancellationToken);
        if (latest is not null && latest.WasIssuedRecently(now))
        {
            return;
        }

        GeneratedSecret secret = secrets.Generate();
        await verifications.AddAsync(EmailVerificationToken.Issue(user.Id, secret.Hash, now), cancellationToken);
        await notifier.SendAsync(email, secret.Value, cancellationToken);
    }
}