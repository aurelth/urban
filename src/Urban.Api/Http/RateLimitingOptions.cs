namespace Urban.Api.Http;

/// <summary>Limites de requisições por cliente das rotas de acesso, de sessão e de termos (seção "RateLimiting"). Sem valores, a API não sobe.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitRule Register { get; init; } = new();

    public RateLimitRule ConfirmEmail { get; init; } = new();

    public RateLimitRule Login { get; init; } = new();

    public RateLimitRule ForgotPassword { get; init; } = new();

    public RateLimitRule ResetPassword { get; init; } = new();

    public RateLimitRule Refresh { get; init; } = new();

    public RateLimitRule Logout { get; init; } = new();

    public RateLimitRule Me { get; init; } = new();

    public RateLimitRule Terms { get; init; } = new();

    public RateLimitRule CondominiumRegistration { get; init; } = new();

    public RateLimitRule PostalCodeLookup { get; init; } = new();

    public RateLimitRule CondominiumProfile { get; init; } = new();

    public RateLimitRule SensitiveChange { get; init; } = new();

    /// <exception cref="InvalidOperationException">A configuração é inválida.</exception>
    public void Validate()
    {
        Validate(nameof(Register), Register);
        Validate(nameof(ConfirmEmail), ConfirmEmail);
        Validate(nameof(Login), Login);
        Validate(nameof(ForgotPassword), ForgotPassword);
        Validate(nameof(ResetPassword), ResetPassword);
        Validate(nameof(Refresh), Refresh);
        Validate(nameof(Logout), Logout);
        Validate(nameof(Me), Me);
        Validate(nameof(Terms), Terms);
        Validate(nameof(CondominiumRegistration), CondominiumRegistration);
        Validate(nameof(PostalCodeLookup), PostalCodeLookup);
        Validate(nameof(CondominiumProfile), CondominiumProfile);
        Validate(nameof(SensitiveChange), SensitiveChange);
    }

    private static void Validate(string route, RateLimitRule rule)
    {
        if (rule.PermitLimit < 1)
        {
            throw new InvalidOperationException($"O limite de requisições ({SectionName}:{route}:{nameof(RateLimitRule.PermitLimit)}) precisa ser maior que zero.");
        }

        if (rule.Window <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"A janela do limite de requisições ({SectionName}:{route}:{nameof(RateLimitRule.Window)}) precisa ser maior que zero.");
        }
    }
}