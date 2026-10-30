using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.EmailChange.ConfirmEmailChange;
using Urban.Identity.Application.EmailChange.RequestEmailChange;
using Urban.Identity.Application.PasswordChange;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>Ações de quem já está logado sobre a própria conta: trocar a senha e trocar o e-mail.</summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder account = app.MapGroup(AuthRoutes.Prefix).WithTags("Conta");

        account.MapPost("/change-password", ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangePassword")
            .WithSummary("Troca a senha de quem está logado")
            .WithDescription(
                "Exige a senha atual e a nova. A nova senha é validada antes (senha fraca, 400, ou igual à atual, 400, não gasta tentativa). "
                + "Senha atual errada responde 403 e conta para o bloqueio da conta (cinco erros bloqueiam por 15 minutos, 423). "
                + "Ao gravar, todas as sessões da pessoa terminam, inclusive a de quem trocou, e o e-mail da conta recebe um aviso. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.SensitiveChange)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        account.MapPost("/change-email", ChangeEmailAsync)
            .RequireAuthorization()
            .WithName("ChangeEmail")
            .WithSummary("Pede a troca do e-mail da conta")
            .WithDescription(
                "Exige a senha e o novo endereço. Envia um link de confirmação ao endereço NOVO (válido por 24 horas, uso único) e um aviso ao atual; "
                + "a conta só muda quando o link é aberto. Responde 202 tenha o endereço conta ou não, para não revelar quem tem conta. "
                + "Endereço inválido ou igual ao atual (400) não gasta tentativa de senha; senha errada responde 403 e conta para o bloqueio (423). "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.SensitiveChange)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        account.MapPost("/confirm-email-change", ConfirmEmailChangeAsync)
            .WithName("ConfirmEmailChange")
            .WithSummary("Confirma a troca de e-mail com o link recebido")
            .WithDescription(
                "Não exige sessão: quem abre o link prova que lê a caixa do endereço novo. A conta passa a usar o novo e-mail (204), "
                + "todas as sessões terminam e o endereço anterior recebe um aviso. Link desconhecido, já usado ou de endereço que ficou ocupado "
                + "(400 invalid) e link vencido (400 expired) pedem uma nova troca. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.ConfirmEmail)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result result = await sender.Send(new ChangePasswordCommand(session.UserId, request.CurrentPassword, request.NewPassword), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        // As sessões já foram revogadas; o cookie que sobrou não serve mais, então sai do navegador.
        RefreshTokenCookie.Delete(response);

        return Results.NoContent();
    }

    private static async Task<IResult> ChangeEmailAsync(
        ChangeEmailRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result result = await sender.Send(new RequestEmailChangeCommand(session.UserId, request.Password, request.NewEmail), cancellationToken);

        return result.IsSuccess ? Results.Accepted() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new ConfirmEmailChangeCommand(request.Token ?? string.Empty), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        RefreshTokenCookie.Delete(response);

        return Results.NoContent();
    }
}
