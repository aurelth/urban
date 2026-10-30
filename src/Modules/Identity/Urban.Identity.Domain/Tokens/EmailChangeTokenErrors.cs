using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>Falhas do <see cref="EmailChangeToken"/>. Link desconhecido, já usado ou de conta que não pode entrar têm o mesmo erro.</summary>
public static class EmailChangeTokenErrors
{
    public static readonly Error Invalid =
        new("identity.email_change.invalid", "Link inválido ou já usado. Peça a troca de e-mail de novo");

    public static readonly Error Expired =
        new("identity.email_change.expired", "Link expirado. Peça a troca de e-mail de novo");
}
