using Urban.Identity.Domain.Access;
using Urban.SharedKernel;

namespace Urban.Identity.Domain.Users;

/// <summary>
/// Conta de acesso ao sistema. Guarda o estado e aplica as transições permitidas; quem pode pedir cada
/// transição é decidido pela <see cref="AccessPolicy"/>. A senha não vive aqui: fica atrás de uma porta.
/// </summary>
public sealed class User
{
    private User(Guid id, Email email, UserRole role, Guid? condominiumId, AccountStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        Role = role;
        CondominiumId = condominiumId;
        Status = status;
        CreatedAt = createdAt;
        StatusChangedAt = createdAt;
    }

    /// <summary>Tamanho máximo do motivo de uma suspensão; é também o tamanho da coluna.</summary>
    public const int MaxReasonLength = 500;

    public Guid Id { get; }

    public Email Email { get; private set; }

    public UserRole Role { get; }

    public Guid? CondominiumId { get; private set; }

    /// <summary>Como a pessoa prefere ser chamada. Vem do pedido de cadastro; quem não passou por ele só o tem depois de cadastrar o condomínio.</summary>
    public DisplayName? DisplayName { get; private set; }

    /// <summary>Nome do condomínio informado no pedido de cadastro, para pré-preencher o formulário depois. Nulo em contas que não nasceram de um pedido.</summary>
    public RequestedCondominiumName? RequestedCondominiumName { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset StatusChangedAt { get; private set; }

    public Guid? StatusChangedBy { get; private set; }

    public string? StatusReason { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Cria a conta já pendente de verificação. Morador exige condomínio; SuperAdmin não pode ter; síndico recebe o dele depois.</summary>
    public static Result<User> Register(Email email, UserRole role, Guid? condominiumId, DateTimeOffset now)
    {
        bool mismatch = (role == UserRole.SuperAdmin && condominiumId is not null)
            || (role == UserRole.Resident && condominiumId is null);

        return mismatch
            ? Result.Failure<User>(UserErrors.CondominiumMismatch)
            : Result.Success(new User(Guid.CreateVersion7(), email, role, condominiumId, AccountStatus.PendingVerification, now));
    }

    public static User RequestManagerAccess(Email email, DisplayName name, RequestedCondominiumName condominiumName, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), email, UserRole.Manager, null, AccountStatus.PendingApproval, now)
        {
            DisplayName = name,
            RequestedCondominiumName = condominiumName,
        };

    /// <summary>Vincula o síndico ao condomínio que ele cadastrou. Só o síndico, e só uma vez: depois disso o vínculo não muda por aqui.</summary>
    public Result AssignCondominium(Guid condominiumId, DisplayName displayName)
    {
        if (Role != UserRole.Manager)
        {
            return Result.Failure(UserErrors.NotManager);
        }

        if (CondominiumId is not null)
        {
            return Result.Failure(UserErrors.CondominiumAlreadyAssigned);
        }

        if (condominiumId == Guid.Empty)
        {
            return Result.Failure(UserErrors.CondominiumRequired);
        }

        CondominiumId = condominiumId;
        DisplayName = displayName;
        return Result.Success();
    }

    /// <summary>Muda como a pessoa prefere ser chamada. Só quem já tem condomínio tem o que editar.</summary>
    public Result ChangeDisplayName(DisplayName displayName)
    {
        if (CondominiumId is null)
        {
            return Result.Failure(UserErrors.CondominiumRequired);
        }

        DisplayName = displayName;
        return Result.Success();
    }

    /// <summary>Troca o endereço de e-mail da conta. A prova de que a pessoa lê a caixa nova é exigida antes, pelo link enviado a ela.</summary>
    public Result ChangeEmail(Email newEmail)
    {
        if (newEmail == Email)
        {
            return Result.Failure(UserErrors.EmailUnchanged);
        }

        Email = newEmail;
        return Result.Success();
    }

    public Result Approve(Guid actorId, DateTimeOffset now) =>
        MoveTo(AccountStatus.PendingVerification, [AccountStatus.PendingApproval], actorId, null, now);

    public Result Reject(Guid actorId, DateTimeOffset now) =>
        MoveTo(AccountStatus.Rejected, [AccountStatus.PendingApproval], actorId, null, now);

    public Result ConfirmEmail(DateTimeOffset now) =>
        MoveTo(AccountStatus.Active, [AccountStatus.PendingVerification], Id, null, now);

    public Result Deactivate(Guid actorId, DateTimeOffset now) =>
        MoveTo(AccountStatus.Deactivated, [AccountStatus.Active], actorId, null, now);

    public Result Activate(Guid actorId, DateTimeOffset now) =>
        MoveTo(AccountStatus.Active, [AccountStatus.Deactivated], actorId, null, now);

    public Result Suspend(Guid actorId, string? reason, DateTimeOffset now)
    {
        string trimmed = (reason ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return Result.Failure(UserErrors.ReasonRequired);
        }

        return trimmed.Length > MaxReasonLength
            ? Result.Failure(UserErrors.ReasonTooLong)
            : MoveTo(AccountStatus.Suspended, [AccountStatus.Active, AccountStatus.Deactivated], actorId, trimmed, now);
    }

    public Result Reactivate(Guid actorId, DateTimeOffset now) =>
        MoveTo(AccountStatus.Active, [AccountStatus.Suspended], actorId, null, now);

    /// <summary>Exclui anonimizando: o e-mail é trocado por um valor descartável e o <see cref="Id"/> permanece para a auditoria.</summary>
    public Result Delete(Guid actorId, DateTimeOffset now)
    {
        Result result = MoveTo(
            AccountStatus.Deleted,
            [AccountStatus.PendingVerification, AccountStatus.Active, AccountStatus.Deactivated, AccountStatus.Suspended],
            actorId,
            null,
            now);

        if (result.IsSuccess)
        {
            Email = Email.Create($"deleted-{Id:N}@deleted.invalid").Value;
            DeletedAt = now;
        }

        return result;
    }

    /// <summary>Diz se a conta pode entrar agora. Só deve ser consultado depois de a senha ser conferida.</summary>
    public Result CheckCanSignIn() => Status switch
    {
        AccountStatus.Active => Result.Success(),
        AccountStatus.PendingApproval => Result.Failure(UserErrors.RegistrationPending),
        AccountStatus.Rejected => Result.Failure(UserErrors.RegistrationRejected),
        AccountStatus.PendingVerification => Result.Failure(UserErrors.EmailNotVerified),
        AccountStatus.Deactivated => Result.Failure(UserErrors.AccountDeactivated),
        AccountStatus.Suspended => Result.Failure(UserErrors.AccountSuspended),
        _ => Result.Failure(UserErrors.AccountDeleted),
    };

    private Result MoveTo(AccountStatus target, AccountStatus[] allowedFrom, Guid changedBy, string? reason, DateTimeOffset now)
    {
        if (!allowedFrom.Contains(Status))
        {
            return Result.Failure(UserErrors.InvalidTransition);
        }

        Status = target;
        StatusChangedAt = now;
        StatusChangedBy = changedBy;
        StatusReason = reason;
        return Result.Success();
    }
}
