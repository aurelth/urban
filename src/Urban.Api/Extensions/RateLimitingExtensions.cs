using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Urban.Api.Http;

namespace Urban.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddUrbanRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        RateLimitingOptions options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        options.Validate();

        services.AddRateLimiter(limiter =>
        {
            limiter.OnRejected = RespondTooManyRequestsAsync;
            limiter.AddPolicy(RateLimitPolicies.Register, http => PerClient(http, options.Register));
            limiter.AddPolicy(RateLimitPolicies.ConfirmEmail, http => PerClient(http, options.ConfirmEmail));
            limiter.AddPolicy(RateLimitPolicies.Login, http => PerClient(http, options.Login));
            limiter.AddPolicy(RateLimitPolicies.ForgotPassword, http => PerClient(http, options.ForgotPassword));
            limiter.AddPolicy(RateLimitPolicies.ResetPassword, http => PerClient(http, options.ResetPassword));
            limiter.AddPolicy(RateLimitPolicies.Refresh, http => PerClient(http, options.Refresh));
            limiter.AddPolicy(RateLimitPolicies.Logout, http => PerClient(http, options.Logout));
            limiter.AddPolicy(RateLimitPolicies.Me, http => PerClient(http, options.Me));
            limiter.AddPolicy(RateLimitPolicies.Terms, http => PerClient(http, options.Terms));
            limiter.AddPolicy(RateLimitPolicies.CondominiumRegistration, http => PerClient(http, options.CondominiumRegistration));
        });

        return services;
    }

    private static RateLimitPartition<string> PerClient(HttpContext http, RateLimitRule rule) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientPartition.KeyOf(http.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rule.PermitLimit,
                Window = rule.Window,
                QueueLimit = 0,
            });

    private static async ValueTask RespondTooManyRequestsAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await Results.Problem(
            detail: "Muitas requisições em pouco tempo. Aguarde um instante e tente de novo",
            statusCode: StatusCodes.Status429TooManyRequests,
            title: ReasonPhrases.GetReasonPhrase(StatusCodes.Status429TooManyRequests),
            extensions: new Dictionary<string, object?> { ["code"] = RateLimitPolicies.ExceededCode })
            .ExecuteAsync(context.HttpContext);
    }
}