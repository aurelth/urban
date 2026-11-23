using Microsoft.AspNetCore.Authentication.JwtBearer;
using Urban.Api.Hubs;
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
                    OnMessageReceived = ReadHubTokenFromQuery,
                    OnTokenValidated = RejectRevokedAsync,
                    OnChallenge = RespondUnauthenticatedAsync,
                };
            });

        return services;
    }

    /// <summary>
    /// O WebSocket do navegador não leva cabeçalho Authorization: o SignalR manda o token de acesso em <c>access_token</c> na URL. Só
    /// vale nas rotas dos hubs; nas demais rotas a URL nunca é fonte de token.
    /// </summary>
    private static Task ReadHubTokenFromQuery(MessageReceivedContext context)
    {
        string? token = context.Request.Query["access_token"];

        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(RealtimeExtensions.HubsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Token = token;
        }

        return Task.CompletedTask;
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