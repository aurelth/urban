namespace Urban.Api.Http;

/// <summary>Limites de requisições por cliente das rotas públicas (seção "RateLimiting"). Sem valores, a API não sobe.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitRule Register { get; init; } = new();

    public RateLimitRule ConfirmEmail { get; init; } = new();

    public RateLimitRule Login { get; init; } = new();

    public RateLimitRule ForgotPassword { get; init; } = new();

    public RateLimitRule ResetPassword { get; init; } = new();

    /// <exception cref="InvalidOperationException">A configuração é inválida.</exception>
    public void Validate()
    {
        Validate(nameof(Register), Register);
        Validate(nameof(ConfirmEmail), ConfirmEmail);
        Validate(nameof(Login), Login);
        Validate(nameof(ForgotPassword), ForgotPassword);
        Validate(nameof(ResetPassword), ResetPassword);
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