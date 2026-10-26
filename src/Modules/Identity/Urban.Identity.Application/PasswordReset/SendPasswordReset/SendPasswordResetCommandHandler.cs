using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.PasswordReset.SendPasswordReset;

/// <summary>
/// Roda em segundo plano: emite o link e o envia. O segredo existe só na memória deste processo; o banco e a fila nunca o veem.
/// Se o provedor falha, o erro sobe para o mecanismo de retentativa, que chama este comando de novo (e cada retentativa emite um link novo).
/// Confere de novo se a conta ainda está ativa e se já não saiu um link há pouco, porque o pedido pode ter sido enfileirado várias vezes.
/// </summary>
public sealed class SendPasswordResetCommandHandler(
    IUserAccounts accounts,
    IPasswordResets resets,
    ISecretGenerator secrets,
    IPasswordResetNotifier notifier,
    IClock clock) : IRequestHandler<SendPasswordResetCommand, Result>
{
    public async Task<Result> Handle(SendPasswordResetCommand request, CancellationToken cancellationToken)
    {
        User? user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            return Result.Success();
        }

        DateTimeOffset now = clock.UtcNow;
        if (await resets.HasRecentLinkAsync(user.Id, now, cancellationToken))
        {
            return Result.Success();
        }

        GeneratedSecret secret = secrets.Generate();
        await resets.AddAsync(PasswordResetToken.Issue(user.Id, secret.Hash, now), cancellationToken);
        await notifier.SendAsync(user.Email, secret.Value, cancellationToken);

        return Result.Success();
    }
}