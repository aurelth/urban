using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Dá acesso ao sistema a um morador já cadastrado. Na primeira vez, cria a conta no Identity e a liga à pessoa na mesma transação
/// (<see cref="IAtomicWork"/>): ou as duas coisas acontecem ou nenhuma. O e-mail do convite só é pedido depois do commit, porque a
/// tarefa em segundo plano pode rodar antes dele e não acharia a conta. Se o pedido do e-mail falhar depois disso, a conta já
/// existe e o síndico repete o convite, que cai no reenvio. Quem já tem conta só recebe o convite de novo.
/// </summary>
public sealed class InviteResidentCommandHandler(
    IResidents residents,
    IResidentAccess access,
    IAtomicWork atomicWork,
    ITenantContext tenant,
    IClock clock) : IRequestHandler<InviteResidentCommand, Result>
{
    public async Task<Result> Handle(InviteResidentCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure(CondominiumErrors.NotFound);
        }

        ResidentRecord? record = await residents.FindAsync(request.ResidentId, cancellationToken);
        if (record is null)
        {
            return Result.Failure(ResidentErrors.NotFound);
        }

        Person person = record.Person;
        if (person.Email is not { } email)
        {
            return Result.Failure(ResidentErrors.EmailRequiredForInvitation);
        }

        Guid userId;
        if (person.UserId is { } existing)
        {
            userId = existing;
        }
        else
        {
            Guid created = Guid.Empty;
            Result linked = await atomicWork.ExecuteAsync(
                async ct =>
                {
                    Result<Guid> account = await access.CreateAsync(condominiumId, email.Value, request.ActorUserId, ct);
                    if (account.IsFailure)
                    {
                        return Result.Failure(account.Error);
                    }

                    Result attached = person.AttachUser(account.Value);
                    if (attached.IsFailure)
                    {
                        return attached;
                    }

                    ChangeLogEntry entry = new(condominiumId, request.ActorUserId, ChangeKind.Resident, ["access"], clock.UtcNow, person.Id);
                    Result<string> saved = await residents.SaveAsync(record, entry, ct);
                    if (saved.IsFailure)
                    {
                        return Result.Failure(saved.Error);
                    }

                    created = account.Value;

                    return Result.Success();
                },
                cancellationToken);
            if (linked.IsFailure)
            {
                return linked;
            }

            userId = created;
        }

        return await access.SendInvitationAsync(userId, condominiumId, request.ActorUserId, cancellationToken);
    }
}
