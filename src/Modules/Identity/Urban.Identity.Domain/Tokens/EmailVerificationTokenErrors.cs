using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>Falhas do <see cref="EmailVerificationToken"/>. Link desconhecido e link já usado têm o mesmo erro.</summary>
public static class EmailVerificationTokenErrors
{
    public static readonly Error Invalid =
        new("identity.email_verification.invalid", "Link de confirmação inválido. Faça o cadastro novamente");

    public static readonly Error Expired =
        new("identity.email_verification.expired", "Link de confirmação expirado. Faça o cadastro novamente para receber outro");
}