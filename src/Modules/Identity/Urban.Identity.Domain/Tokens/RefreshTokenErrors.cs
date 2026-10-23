using Urban.SharedKernel;

namespace Urban.Identity.Domain.Tokens;

/// <summary>Falhas de negócio do <see cref="RefreshToken"/>. <see cref="Reused"/> indica possível roubo: quem chama deve revogar todos os tokens do usuário.</summary>
public static class RefreshTokenErrors
{
    public static readonly Error Expired =
        new("identity.refresh_token.expired", "Sessão expirada. Entre novamente");

    public static readonly Error Reused =
        new("identity.refresh_token.reused", "Sessão inválida. Entre novamente");
}
