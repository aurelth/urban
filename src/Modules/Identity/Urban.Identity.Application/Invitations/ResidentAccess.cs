using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Invitations;

/// <summary>
/// Implementação do contrato que o Condomínio usa para dar acesso a um morador. O síndico é lido de novo da conta (o token só
/// diz quem ele era ao entrar) e a regra de quem pode convidar é da <see cref="AccessPolicy"/>. A conta nasce pendente, sem senha:
/// quem escolhe a senha é o morador, pelo link do e-mail, e só então a conta fica ativa.
/// </summary>
public sealed class ResidentAccess(
    IUserAccounts accounts,
    IEmailVerifications verifications,
    IResidentInvitationDispatcher dispatcher,
    IClock clock) : IResidentAccess
{
    public async Task<Result<Guid>> CreateAsync(Guid condominiumId, string email, Guid invitedBy, CancellationToken cancellationToken)
    {
        Result allowed = await AuthorizeAsync(invitedBy, new AccessSubject(Guid.Empty, UserRole.Resident, condominiumId), cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<Guid>(allowed.Error);
        }

        Result<Email> address = Email.Create(email);
        if (address.IsFailure)
        {
            return Result.Failure<Guid>(address.Error);
        }

        if (await accounts.FindByEmailAsync(address.Value, cancellationToken) is not null)
        {
            return Result.Failure<Guid>(ResidentAccessErrors.EmailUnavailable);
        }

        Result<User> user = User.Register(address.Value, UserRole.Resident, condominiumId, clock.UtcNow);
        if (user.IsFailure)
        {
            return Result.Failure<Guid>(user.Error);
        }

        // A conferência acima responde rápido; se outro cadastro ganhar a corrida, o índice único do e-mail decide.
        return await verifications.TryRegisterAsync(user.Value, cancellationToken)
            ? Result.Success(user.Value.Id)
            : Result.Failure<Guid>(ResidentAccessErrors.EmailUnavailable);
    }

    public async Task<Result> SendInvitationAsync(Guid userId, Guid condominiumId, Guid invitedBy, CancellationToken cancellationToken)
    {
        User? resident = await accounts.FindByIdAsync(userId, cancellationToken);
        if (resident is null || resident.Role != UserRole.Resident || resident.CondominiumId != condominiumId)
        {
            return Result.Failure(ResidentAccessErrors.NotFound);
        }

        Result allowed = await AuthorizeAsync(invitedBy, new AccessSubject(resident.Id, resident.Role, resident.CondominiumId), cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        if (resident.Status != AccountStatus.PendingVerification)
        {
            return Result.Failure(ResidentAccessErrors.NotPending);
        }

        if (await verifications.HasRecentLinkAsync(resident.Id, clock.UtcNow, cancellationToken))
        {
            return Result.Failure(ResidentAccessErrors.ResendTooSoon);
        }

        await dispatcher.EnqueueAsync(resident.Id, cancellationToken);

        return Result.Success();
    }

    public async Task<IReadOnlyDictionary<Guid, ResidentAccessStatus>> GetStatusesAsync(Guid condominiumId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, ResidentAccessStatus>();
        }

        IReadOnlyDictionary<Guid, AccountStatus> found = await accounts.ListStatusesAsync(condominiumId, userIds, cancellationToken);

        return found.ToDictionary(pair => pair.Key, pair => ToAccessStatus(pair.Value));
    }

    public async Task<Result> ChangeInvitedEmailAsync(Guid userId, Guid condominiumId, string newEmail, Guid changedBy, CancellationToken cancellationToken)
    {
        User? resident = await accounts.FindByIdAsync(userId, cancellationToken);
        if (resident is null || resident.Role != UserRole.Resident || resident.CondominiumId != condominiumId)
        {
            return Result.Failure(ResidentAccessErrors.NotFound);
        }

        Result allowed = await AuthorizeAsync(changedBy, new AccessSubject(resident.Id, resident.Role, resident.CondominiumId), cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        if (resident.Status != AccountStatus.PendingVerification)
        {
            return Result.Failure(ResidentAccessErrors.NotPending);
        }

        Result<Email> address = Email.Create(newEmail);
        if (address.IsFailure)
        {
            return Result.Failure(address.Error);
        }

        if (address.Value == resident.Email)
        {
            return Result.Success();
        }

        if (await accounts.FindByEmailAsync(address.Value, cancellationToken) is not null)
        {
            return Result.Failure(ResidentAccessErrors.EmailUnavailable);
        }

        Result changed = resident.ChangeEmail(address.Value);
        if (changed.IsFailure)
        {
            return changed;
        }

        // A conferência acima responde rápido; se outra conta ganhar a corrida pelo endereço, o índice único do e-mail decide.
        return await verifications.ChangePendingEmailAsync(resident, clock.UtcNow, cancellationToken)
            ? Result.Success()
            : Result.Failure(ResidentAccessErrors.EmailUnavailable);
    }

    private static ResidentAccessStatus ToAccessStatus(AccountStatus status) => status switch
    {
        AccountStatus.PendingVerification => ResidentAccessStatus.Invited,
        AccountStatus.Active => ResidentAccessStatus.Active,
        _ => ResidentAccessStatus.Inactive,
    };

    private async Task<Result> AuthorizeAsync(Guid actorId, AccessSubject target, CancellationToken cancellationToken)
    {
        User? actor = await accounts.FindByIdAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return Result.Failure(ResidentAccessErrors.Forbidden);
        }

        Result canSignIn = actor.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return canSignIn;
        }

        AccessSubject subject = new(actor.Id, actor.Role, actor.CondominiumId);

        return AccessPolicy.CanManage(subject, ManagementAction.Create, target)
            ? Result.Success()
            : Result.Failure(ResidentAccessErrors.Forbidden);
    }
}
