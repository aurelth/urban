using Urban.SharedKernel;

namespace Urban.Identity.Domain.Users;

/// <summary>Falhas de negócio do agregado <see cref="User"/>. Os códigos são estáveis: o frontend e os testes dependem deles.</summary>
public static class UserErrors
{
    public static readonly Error InvalidTransition =
        new("identity.user.invalid_transition", "Esta ação não é possível no estado atual da conta");

    public static readonly Error ReasonRequired =
        new("identity.user.reason_required", "Informe o motivo");

    public static readonly Error CondominiumMismatch =
        new("identity.user.condominium_mismatch", "O papel e o condomínio do usuário não combinam");

    public static readonly Error NotManager =
        new("identity.user.not_manager", "Só o síndico pode cadastrar o condomínio");

    public static readonly Error CondominiumAlreadyAssigned =
        new("identity.user.condominium_already_assigned", "Esta conta já tem um condomínio cadastrado");

    public static readonly Error CondominiumRequired =
        new("identity.user.condominium_required", "Informe o condomínio");

    public static readonly Error EmailNotVerified =
        new("identity.user.email_not_verified", "Confirme seu e-mail para entrar");

    public static readonly Error AccountDeactivated =
        new("identity.user.account_deactivated", "Conta desativada. Fale com o síndico");

    public static readonly Error AccountSuspended =
        new("identity.user.account_suspended", "Acesso suspenso. Fale com o suporte");

    public static readonly Error AccountDeleted =
        new("identity.user.account_deleted", "Conta excluída");

    public static readonly Error PasswordIncorrect =
        new("identity.password.incorrect", "Senha incorreta");

    public static readonly Error PasswordUnchanged =
        new("identity.password.unchanged", "A nova senha precisa ser diferente da atual");

    public static readonly Error EmailUnchanged =
        new("identity.email.unchanged", "Este já é o e-mail da sua conta");
}
