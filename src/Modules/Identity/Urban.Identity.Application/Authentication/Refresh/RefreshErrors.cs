using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication.Refresh;

public static class RefreshErrors
{
    /// <summary>Token desconhecido ou de usuário que não existe mais: a pessoa precisa entrar de novo.</summary>
    public static readonly Error InvalidSession =
        new("identity.refresh.invalid_session", "Sessão inválida. Entre novamente");
}
