using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>Falhas do <see cref="PasswordResetToken"/>. Link desconhecido, já usado ou de conta que não pode entrar têm o mesmo erro.</summary>
public static class PasswordResetTokenErrors
{
    public static readonly Error Invalid =
        new("identity.password_reset.invalid", "Link de redefinição inválido. Peça um novo link");

    public static readonly Error Expired =
        new("identity.password_reset.expired", "Link de redefinição expirado. Peça um novo link");
}