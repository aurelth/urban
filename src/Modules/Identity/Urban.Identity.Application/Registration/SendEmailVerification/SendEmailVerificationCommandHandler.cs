using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Registration.SendEmailVerification;

/// <summary>
/// Roda em segundo plano: emite o link e o envia. O segredo existe só na memória deste processo; o banco e a fila nunca o veem.
/// Se o provedor falha, o erro sobe para o mecanismo de retentativa, que chama este comando de novo (e cada retentativa emite um link novo).
/// Confere de novo se a conta ainda está pendente e se já não saiu um link há pouco, porque o pedido pode ter sido enfileirado várias vezes.
/// </summary>
public sealed class SendEmailVerificationCommandHandler(
    IUserAccounts accounts,
    IEmailVerifications verifications,
    ISecretGenerator secrets,
    IEmailVerificationNotifier notifier,
    IClock clock) : IRequestHandler<SendEmailVerificationCommand, Result>
{
    public async Task<Result> Handle(SendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        User? user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.PendingVerification)
        {
            return Result.Success();
        }

        DateTimeOffset now = clock.UtcNow;
        if (await verifications.HasRecentLinkAsync(user.Id, now, cancellationToken))
        {
            return Result.Success();
        }

        GeneratedSecret secret = secrets.Generate();
        await verifications.AddAsync(EmailVerificationToken.Issue(user.Id, secret.Hash, now), cancellationToken);
        await notifier.SendAsync(user.Email, secret.Value, cancellationToken);

        return Result.Success();
    }
}