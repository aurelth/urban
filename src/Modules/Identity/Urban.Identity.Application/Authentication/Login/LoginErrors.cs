using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication.Login;

public static class LoginErrors
{
    /// <summary>Mesmo erro para e-mail inexistente, e-mail malformado e senha errada: não revela quais e-mails existem.</summary>
    public static readonly Error InvalidCredentials =
        new("identity.login.invalid_credentials", "E-mail ou senha incorretos");

    public static readonly Error LockedOut =
        new("identity.login.locked_out", "Conta bloqueada por tentativas excessivas. Tente novamente em alguns minutos");
}
