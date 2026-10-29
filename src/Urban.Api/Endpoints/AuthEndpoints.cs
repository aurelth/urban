using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.Authentication.Logout;
using Urban.Identity.Application.Authentication.Me;
using Urban.Identity.Application.Authentication.Refresh;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder auth = app.MapGroup(AuthRoutes.Prefix).WithTags("Autenticação");

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Entra com e-mail e senha")
            .WithDescription(
                "Devolve o token de acesso no corpo e grava o token de renovação em cookie HttpOnly. "
                + "E-mail inexistente e senha errada recebem a mesma resposta (401). "
                + "O estado da conta (403) só é revelado depois da senha correta. "
                + "Cinco senhas erradas bloqueiam a conta por 15 minutos (423). "
                + "O mesmo IP tem um limite de tentativas por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Login)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .WithSummary("Renova a sessão com o cookie de renovação")
            .WithDescription(
                "Troca o token de renovação do cookie por um novo par (rotação). Reapresentar um token já trocado "
                + "derruba todas as sessões da pessoa. Em qualquer falha o cookie é apagado. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Refresh)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Encerra a sessão atual")
            .WithDescription(
                "Exige o token de acesso no cabeçalho Authorization. Revoga o token de acesso e o token de renovação "
                + "do cookie e apaga o cookie. Sempre responde 204 para quem está autenticado, exista ou não o token de renovação. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Logout)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        auth.MapGet("/me", MeAsync)
            .RequireAuthorization()
            .WithName("Me")
            .WithSummary("Informa quem está logado")
            .WithDescription(
                "Exige o token de acesso no cabeçalho Authorization. Devolve identificador, e-mail, papel e condomínio. "
                + "A conta é lida de novo a cada chamada: se foi suspensa ou desativada depois do login, responde 403 com o motivo. "
                + "A resposta nunca é guardada em cache. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Me)
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        ISender sender,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        Result<AuthTokens> result = await sender.Send(
            new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Issue(response, result.Value) : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> RefreshAsync(
        HttpRequest request,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        string? token = RefreshTokenCookie.Read(request);

        Result<AuthTokens> result = string.IsNullOrEmpty(token)
            ? Result.Failure<AuthTokens>(RefreshErrors.InvalidSession)
            : await sender.Send(new RefreshCommand(token), cancellationToken);

        if (result.IsFailure)
        {
            RefreshTokenCookie.Delete(response);
            return ErrorHttp.ToProblem(result.Error);
        }

        return Issue(response, result.Value);
    }

    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal user,
        HttpRequest request,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        await sender.Send(
            new LogoutCommand(session.UserId, session.TokenId, session.ExpiresAt, RefreshTokenCookie.Read(request)),
            cancellationToken);
        RefreshTokenCookie.Delete(response);

        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result<CurrentUser> result = await sender.Send(new GetCurrentUserQuery(session.UserId), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Ok(AuthMapper.ToResponse(result.Value));
    }

    private static IResult Issue(HttpResponse response, AuthTokens tokens)
    {
        RefreshTokenCookie.Append(response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        response.PreventCaching();

        return Results.Ok(AuthMapper.ToResponse(tokens));
    }
}