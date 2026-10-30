using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Application.Profile;

/// <summary>
/// Edição dos campos sensíveis. A ordem importa: primeiro os dados são validados (campo inválido não gasta tentativa de
/// senha), depois a senha é confirmada FORA da unidade atômica, para que uma tentativa errada continue contada no bloqueio
/// da conta mesmo que o resto seja desfeito. Só então o cadastro é lido de novo, conferido contra a versão e gravado.
/// </summary>
public sealed class UpdateManagerIdentityCommandHandler(
    ICondominiumProfiles profiles,
    IPasswordConfirmation passwordConfirmation,
    IAtomicWork atomicWork,
    IClock clock) : IRequestHandler<UpdateManagerIdentityCommand, Result<MyCondominium>>
{
    // O Brasil não tem horário de verão desde 2019, então a data local é o horário UTC menos três horas.
    private static readonly TimeSpan BrazilOffset = TimeSpan.FromHours(-3);

    public async Task<Result<MyCondominium>> Handle(UpdateManagerIdentityCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<MyCondominium>(CondominiumErrors.VersionRequired);
        }

        DateTimeOffset now = clock.UtcNow;
        Result<Edit> edit = Build(request, now);
        if (edit.IsFailure)
        {
            return Result.Failure<MyCondominium>(edit.Error);
        }

        Result confirmed = await passwordConfirmation.ConfirmAsync(request.ManagerUserId, request.Password, cancellationToken);
        if (confirmed.IsFailure)
        {
            return Result.Failure<MyCondominium>(confirmed.Error);
        }

        ManagerCondominium? registration = await profiles.FindOfManagerAsync(request.ManagerUserId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<MyCondominium>(CondominiumErrors.NotFound);
        }

        if (request.Version != registration.Version)
        {
            return Result.Failure<MyCondominium>(CondominiumErrors.ConcurrencyConflict);
        }

        List<string> changed = Changes(registration.Manager, edit.Value);
        if (changed.Count == 0)
        {
            return Result.Success(MyCondominium.From(registration, null));
        }

        Result<MyCondominium>? outcome = null;
        Result atomic = await atomicWork.ExecuteAsync(
            async ct =>
            {
                outcome = await ApplyAsync(registration, edit.Value, request.ManagerUserId, changed, now, ct);
                return outcome;
            },
            cancellationToken);

        return atomic.IsSuccess ? outcome! : Result.Failure<MyCondominium>(atomic.Error);
    }

    private static Result<Edit> Build(UpdateManagerIdentityCommand request, DateTimeOffset now)
    {
        DateOnly today = DateOnly.FromDateTime(now.ToOffset(BrazilOffset).DateTime);

        Result<PersonName> fullName = PersonName.Create(request.FullName);
        Result<BirthDate> birthDate = BirthDate.Create(request.BirthDate, today);
        Result<Cpf>? cpf = string.IsNullOrWhiteSpace(request.Cpf) ? null : Cpf.Create(request.Cpf);

        foreach (Result part in new Result?[] { fullName, birthDate, cpf }.OfType<Result>())
        {
            if (part.IsFailure)
            {
                return Result.Failure<Edit>(part.Error);
            }
        }

        return Result.Success(new Edit(fullName.Value, birthDate.Value, cpf?.Value));
    }

    private static List<string> Changes(Person manager, Edit edit)
    {
        List<string> changed = [];
        if (edit.FullName != manager.FullName)
        {
            changed.Add("fullName");
        }

        if (edit.BirthDate != manager.BirthDate)
        {
            changed.Add("birthDate");
        }

        if (edit.Cpf is not null && edit.Cpf != manager.Cpf)
        {
            changed.Add("cpf");
        }

        return changed;
    }

    private async Task<Result<MyCondominium>> ApplyAsync(
        ManagerCondominium registration,
        Edit edit,
        Guid managerUserId,
        List<string> changed,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        registration.Manager.UpdateIdentity(edit.FullName, edit.Cpf ?? registration.Manager.Cpf, edit.BirthDate);

        ChangeLogEntry entry = new(registration.Condominium.Id, managerUserId, ChangeKind.Identity, changed, now);
        Result<string> saved = await profiles.SaveAsync(registration, entry, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(MyCondominium.From(registration, null, saved.Value))
            : Result.Failure<MyCondominium>(saved.Error);
    }

    private sealed record Edit(PersonName FullName, BirthDate BirthDate, Cpf? Cpf);
}
