using Microsoft.AspNetCore.Authentication.JwtBearer;
using Urban.Api.Http;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Infrastructure.Tokens;

namespace Urban.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddUrbanAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtOptions>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = AccessTokenValidation.CreateParameters(jwt);
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = RejectRevokedAsync,
                    OnChallenge = RespondUnauthenticatedAsync,
                };
            });

        return services;
    }

    private static async Task RejectRevokedAsync(TokenValidatedContext context)
    {
        if (context.Principal is null || !AccessTokenSession.TryRead(context.Principal, out AccessTokenSession? session))
        {
            context.Fail(SessionErrors.Unauthenticated.Description);
            return;
        }

        ISessionRevocationChecker revocations = context.HttpContext.RequestServices.GetRequiredService<ISessionRevocationChecker>();
        bool revoked = await revocations.IsRevokedAsync(
            session.TokenId, session.UserId, session.IssuedAt, context.HttpContext.RequestAborted);

        if (revoked)
        {
            context.Fail(SessionErrors.Unauthenticated.Description);
        }
    }

    private static Task RespondUnauthenticatedAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;

        return ErrorHttp.ToProblem(SessionErrors.Unauthenticated).ExecuteAsync(context.HttpContext);
    }
}