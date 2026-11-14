using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Invitations.SendResidentInvitation;

/// <summary>
/// Roda em segundo plano: emite o link do convite (72 horas) e o envia. O segredo existe só na memória deste processo; o banco e a
/// fila nunca o veem. Se o provedor falha, o erro sobe para o mecanismo de retentativa, que chama este comando de novo. Confere de
/// novo se a conta ainda é de um morador pendente e se já não saiu um link há pouco, porque o pedido pode ter sido enfileirado
/// várias vezes.
/// </summary>
public sealed class SendResidentInvitationCommandHandler(
    IUserAccounts accounts,
    IEmailVerifications verifications,
    ISecretGenerator secrets,
    IResidentInvitationNotifier notifier,
    ICondominiumDirectory condominiums,
    IClock clock) : IRequestHandler<SendResidentInvitationCommand, Result>
{
    public async Task<Result> Handle(SendResidentInvitationCommand request, CancellationToken cancellationToken)
    {
        User? resident = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (resident is not { Role: UserRole.Resident, Status: AccountStatus.PendingVerification, CondominiumId: { } condominiumId })
        {
            return Result.Success();
        }

        DateTimeOffset now = clock.UtcNow;
        if (await verifications.HasRecentLinkAsync(resident.Id, now, cancellationToken))
        {
            return Result.Success();
        }

        IReadOnlyDictionary<Guid, string> names = await condominiums.GetNamesAsync([condominiumId], cancellationToken);
        GeneratedSecret secret = secrets.Generate();
        await verifications.AddAsync(EmailVerificationToken.IssueInvitation(resident.Id, secret.Hash, now), cancellationToken);
        await notifier.SendAsync(resident.Email, names.GetValueOrDefault(condominiumId), secret.Value, cancellationToken);

        return Result.Success();
    }
}
