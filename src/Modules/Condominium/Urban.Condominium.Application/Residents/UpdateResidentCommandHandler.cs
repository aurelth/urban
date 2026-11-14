using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Edita os dados pessoais de um morador. Valida e compara em memória; se nada mudou, não grava nada (nem a versão muda). O
/// registro leva só os nomes dos campos que mudaram, nunca os valores. O CPF repetido é decidido na gravação, pelo índice único.
/// Depois do convite o e-mail é o da conta de acesso: enquanto a pessoa ainda não escolheu a senha ele se corrige (a conta e o
/// cadastro mudam juntos, na mesma transação); depois disso trava, porque é o e-mail do login.
/// </summary>
public sealed class UpdateResidentCommandHandler(
    IResidents residents,
    IResidentAccess access,
    IAtomicWork atomicWork,
    ResidentViews views,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<UpdateResidentCommand, Result<ResidentView>>
{
    public async Task<Result<ResidentView>> Handle(UpdateResidentCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ResidentView>(CondominiumErrors.NotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<ResidentView>(CondominiumErrors.VersionRequired);
        }

        ResidentRecord? record = await residents.FindAsync(request.ResidentId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<ResidentView>(ResidentErrors.NotFound);
        }

        if (request.Version != record.Version)
        {
            return Result.Failure<ResidentView>(CondominiumErrors.ConcurrencyConflict);
        }

        Person person = record.Person;
        DateTimeOffset now = clock.UtcNow;
        Result<ResidentData> parsed = ResidentData.Parse(
            request.FullName,
            request.Cpf,
            request.BirthDate,
            request.Phone,
            request.Email,
            request.Profession,
            request.EmergencyContact,
            BrazilCalendar.Today(now),
            person.Cpf);
        if (parsed.IsFailure)
        {
            return Result.Failure<ResidentView>(parsed.Error);
        }

        ResidentData data = parsed.Value;
        if (record.Occupancies.Any(item => item.Occupancy.IsActive && ResidentData.BreaksMinorRule(data.IsMinor, item.Occupancy.Kind)))
        {
            return Result.Failure<ResidentView>(ResidentErrors.UnderAge);
        }

        Guid? invitedAccount = null;
        if (person.UserId is { } accountId && person.Email != data.Email)
        {
            if (data.Email is null || !await IsInvitedAsync(condominiumId, accountId, cancellationToken))
            {
                return Result.Failure<ResidentView>(ResidentErrors.EmailLocked);
            }

            invitedAccount = accountId;
        }

        List<string> changed = Changed(person, data);
        if (changed.Count == 0)
        {
            return Result.Success(await views.ViewAsync(record, cancellationToken));
        }

        Result updated = person.UpdateResident(data.FullName, data.Cpf, data.BirthDate, data.Phone, data.Email, data.Profession, data.EmergencyContact);
        if (updated.IsFailure)
        {
            return Result.Failure<ResidentView>(updated.Error);
        }

        ChangeLogEntry entry = new(condominiumId, request.ActorUserId, ChangeKind.Resident, changed, now, person.Id);
        Result<string> saved = invitedAccount is { } userId
            ? await SaveWithAccountEmailAsync(record, entry, userId, data.Email!.Value, request.ActorUserId, cancellationToken)
            : await residents.SaveAsync(record, entry, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(await views.ViewAsync(record with { Version = saved.Value }, cancellationToken))
            : Result.Failure<ResidentView>(saved.Error);
    }

    private async Task<bool> IsInvitedAsync(Guid condominiumId, Guid userId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ResidentAccessStatus> statuses = await access.GetStatusesAsync(condominiumId, [userId], cancellationToken);

        return statuses.GetValueOrDefault(userId) == ResidentAccessStatus.Invited;
    }

    /// <summary>Muda o e-mail da conta e grava o cadastro numa transação só: ou o login e o cadastro apontam para o endereço novo, ou nenhum dos dois muda.</summary>
    private async Task<Result<string>> SaveWithAccountEmailAsync(
        ResidentRecord record,
        ChangeLogEntry entry,
        Guid userId,
        string email,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        string version = string.Empty;
        Result outcome = await atomicWork.ExecuteAsync(
            async ct =>
            {
                Result moved = await access.ChangeInvitedEmailAsync(userId, entry.CondominiumId, email, actorUserId, ct);
                if (moved.IsFailure)
                {
                    return moved;
                }

                Result<string> saved = await residents.SaveAsync(record, entry, ct);
                if (saved.IsFailure)
                {
                    return Result.Failure(saved.Error);
                }

                version = saved.Value;

                return Result.Success();
            },
            cancellationToken);

        return outcome.IsSuccess ? Result.Success(version) : Result.Failure<string>(outcome.Error);
    }

    /// <summary>Compara os valores já normalizados, então espaço e caixa à toa não contam como mudança.</summary>
    private static List<string> Changed(Person person, ResidentData data)
    {
        List<string> changed = [];
        AddIf(changed, "fullName", person.FullName != data.FullName);
        AddIf(changed, "cpf", person.Cpf != data.Cpf);
        AddIf(changed, "birthDate", person.BirthDate != data.BirthDate);
        AddIf(changed, "phone", person.Phone != data.Phone);
        AddIf(changed, "email", person.Email != data.Email);
        AddIf(changed, "profession", person.Profession != data.Profession);
        AddIf(changed, "emergencyContact", person.EmergencyContact != data.EmergencyContact);

        return changed;
    }

    private static void AddIf(List<string> changed, string field, bool differs)
    {
        if (differs)
        {
            changed.Add(field);
        }
    }
}
