namespace Urban.Api.Http;

public static class RateLimitPolicies
{
    public const string Register = "register";

    public const string ConfirmEmail = "confirm-email";

    public const string Login = "login";

    public const string ForgotPassword = "forgot-password";

    public const string ResetPassword = "reset-password";

    public const string Refresh = "refresh";

    public const string Logout = "logout";

    public const string Me = "me";

    public const string Terms = "terms";

    /// <summary>Código do problema devolvido com o 429, no mesmo campo "code" dos erros de negócio.</summary>
    public const string ExceededCode = "rate_limit.exceeded";
}