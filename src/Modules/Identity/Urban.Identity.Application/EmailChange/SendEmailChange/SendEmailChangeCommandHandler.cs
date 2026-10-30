using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.EmailChange.SendEmailChange;

/// <summary>
/// Roda em segundo plano: emite o link de troca e o envia ao endereço novo. O segredo existe só na memória deste processo;
/// o banco e a fila nunca o veem. Confere de novo o que pode ter mudado desde o pedido: conta ainda ativa, endereço ainda
/// livre e nenhum link emitido há pouco (o pedido pode ter sido enfileirado várias vezes ou repetido por retentativa).
/// </summary>
public sealed class SendEmailChangeCommandHandler(
    IUserAccounts accounts,
    IEmailChanges changes,
    ISecretGenerator secrets,
    IEmailChangeNotifier notifier,
    IClock clock) : IRequestHandler<SendEmailChangeCommand, Result>
{
    public async Task<Result> Handle(SendEmailChangeCommand request, CancellationToken cancellationToken)
    {
        Result<Email> newEmail = Email.Create(request.NewEmail);
        if (newEmail.IsFailure)
        {
            return Result.Success();
        }

        User? user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            return Result.Success();
        }

        DateTimeOffset now = clock.UtcNow;
        if (await changes.HasRecentLinkAsync(user.Id, now, cancellationToken))
        {
            return Result.Success();
        }

        if (await accounts.FindByEmailAsync(newEmail.Value, cancellationToken) is not null)
        {
            return Result.Success();
        }

        GeneratedSecret secret = secrets.Generate();
        await changes.AddAsync(EmailChangeToken.Issue(user.Id, newEmail.Value, secret.Hash, now), cancellationToken);
        await notifier.SendLinkAsync(newEmail.Value, secret.Value, cancellationToken);

        return Result.Success();
    }
}
