using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.Condominium.Domain.Condominiums;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;
using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Application.Profile;

/// <summary>
/// Edição dos campos livres do cadastro. Valida tudo em memória antes de gravar; se nada mudou, não grava nada. O nome de
/// exibição mora na conta (Identity), então ele e o cadastro gravam numa unidade atômica só: se a gravação do cadastro for
/// recusada (outra pessoa editou antes), o nome volta ao que era.
/// </summary>
public sealed class UpdateMyCondominiumCommandHandler(
    ICondominiumProfiles profiles,
    IManagerProfile managerProfile,
    IAtomicWork atomicWork,
    IClock clock) : IRequestHandler<UpdateMyCondominiumCommand, Result<MyCondominium>>
{
    public async Task<Result<MyCondominium>> Handle(UpdateMyCondominiumCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<MyCondominium>(CondominiumErrors.VersionRequired);
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

        Result<Edit> edit = Edit.Build(request);
        if (edit.IsFailure)
        {
            return Result.Failure<MyCondominium>(edit.Error);
        }

        string? currentDisplayName = await managerProfile.GetDisplayNameAsync(request.ManagerUserId, cancellationToken);
        List<string> changed = Changes(registration, edit.Value, request.DisplayName, currentDisplayName);
        if (changed.Count == 0)
        {
            return Result.Success(MyCondominium.From(registration, currentDisplayName));
        }

        Result<MyCondominium>? outcome = null;
        Result atomic = await atomicWork.ExecuteAsync(
            async ct =>
            {
                outcome = await ApplyAsync(registration, edit.Value, request, currentDisplayName, changed, ct);
                return outcome;
            },
            cancellationToken);

        return atomic.IsSuccess ? outcome! : Result.Failure<MyCondominium>(atomic.Error);
    }

    private static List<string> Changes(ManagerCondominium registration, Edit edit, string? requestedName, string? currentName)
    {
        List<string> changed = [];
        Add(changed, "displayName", requestedName is not null && !string.Equals(Squeeze(requestedName), currentName, StringComparison.Ordinal));
        Add(changed, "phone", edit.Phone != registration.Manager.Phone);
        Add(changed, "managerAddress", edit.ManagerAddress != registration.Manager.Address);
        Add(changed, "condominiumName", edit.CondominiumName != registration.Condominium.Name);
        Add(changed, "condominiumAddress", edit.CondominiumAddress != registration.Condominium.Address);

        return changed;
    }

    private static string Squeeze(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void Add(List<string> changed, string field, bool isChanged)
    {
        if (isChanged)
        {
            changed.Add(field);
        }
    }

    private async Task<Result<MyCondominium>> ApplyAsync(
        ManagerCondominium registration,
        Edit edit,
        UpdateMyCondominiumCommand request,
        string? currentDisplayName,
        List<string> changed,
        CancellationToken cancellationToken)
    {
        string? displayName = currentDisplayName;
        if (changed.Contains("displayName"))
        {
            Result<string> renamed = await managerProfile.UpdateDisplayNameAsync(request.ManagerUserId, request.DisplayName, cancellationToken);
            if (renamed.IsFailure)
            {
                return Result.Failure<MyCondominium>(renamed.Error);
            }

            displayName = renamed.Value;
        }

        Result contact = registration.Manager.UpdateContact(edit.Phone, edit.ManagerAddress);
        Result condominium = registration.Condominium.Update(edit.CondominiumName, edit.CondominiumAddress);
        foreach (Result part in new[] { contact, condominium })
        {
            if (part.IsFailure)
            {
                return Result.Failure<MyCondominium>(part.Error);
            }
        }

        ChangeLogEntry entry = new(registration.Condominium.Id, request.ManagerUserId, ChangeKind.Profile, changed, clock.UtcNow);
        Result<string> saved = await profiles.SaveAsync(registration, entry, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(MyCondominium.From(registration, displayName, saved.Value))
            : Result.Failure<MyCondominium>(saved.Error);
    }

    /// <summary>Os campos livres já validados e normalizados, prontos para comparar com o que está gravado.</summary>
    private sealed record Edit(PhoneNumber Phone, Address ManagerAddress, string CondominiumName, Address CondominiumAddress)
    {
        public static Result<Edit> Build(UpdateMyCondominiumCommand request)
        {
            Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);
            Result<Address> managerAddress = CreateAddress(request.ManagerAddress);
            Result<Address> condominiumAddress = CreateAddress(request.CondominiumAddress);

            foreach (Result part in new Result[] { phone, managerAddress, condominiumAddress })
            {
                if (part.IsFailure)
                {
                    return Result.Failure<Edit>(part.Error);
                }
            }

            if (!phone.Value.IsMobile)
            {
                return Result.Failure<Edit>(Domain.People.PersonErrors.MobileRequired);
            }

            Result<string> name = CondominiumEntity.NormalizeName(request.CondominiumName);
            if (name.IsFailure)
            {
                return Result.Failure<Edit>(name.Error);
            }

            return Result.Success(new Edit(phone.Value, managerAddress.Value, name.Value, condominiumAddress.Value));
        }

        private static Result<Address> CreateAddress(AddressInput input) =>
            Address.Create(input.Street, input.Number, input.Complement, input.Neighborhood, input.City, input.State, input.PostalCode);
    }
}
