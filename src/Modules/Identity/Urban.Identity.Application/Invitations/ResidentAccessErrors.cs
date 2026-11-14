using Urban.SharedKernel;

namespace Urban.Identity.Application.Invitations;

public static class ResidentAccessErrors
{
    public static readonly Error Forbidden =
        new("identity.resident_access.forbidden", "Só o síndico com a conta ativa pode dar acesso a moradores do próprio condomínio");

    public static readonly Error EmailUnavailable =
        new("identity.resident_access.email_unavailable", "Este e-mail já tem uma conta no Urban e não pode receber outro convite");

    public static readonly Error NotFound =
        new("identity.resident_access.not_found", "Este morador não tem convite para reenviar");

    public static readonly Error NotPending =
        new("identity.resident_access.not_pending", "O convite só pode ser reenviado enquanto o morador ainda não criou a senha");

    public static readonly Error ResendTooSoon =
        new("identity.resident_access.resend_too_soon", "Um convite acabou de ser enviado. Espere um minuto para enviar outro");
}
