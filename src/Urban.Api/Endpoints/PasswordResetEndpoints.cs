using MediatR;
using Urban.Api.Http;
using Urban.Identity.Application.PasswordReset.ForgotPassword;
using Urban.Identity.Application.PasswordReset.ResetPassword;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class PasswordResetEndpoints
{
    public static IEndpointRouteBuilder MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder passwords = app.MapGroup(AuthRoutes.Prefix).WithTags("Senha");

        passwords.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .WithSummary("Pede o link para redefinir a senha")
            .WithDescription(
                "Recebe só o e-mail e envia o link de redefinição (válido por 1 hora) quando a conta existe e está ativa. "
                + "Responde 202 para qualquer e-mail válido, tenha ele conta ou não, para não revelar quem tem conta. "
                + "Pedir de novo em menos de 1 minuto não envia outro e-mail. "
                + "O mesmo IP tem um limite de pedidos por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.ForgotPassword)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        passwords.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .WithSummary("Define a nova senha com o link recebido")
            .WithDescription(
                "Troca o token do link e a nova senha (204). O link vale uma vez só e, ao ser usado, inutiliza os outros links abertos da conta. "
                + "A senha é validada antes do token: senha fraca (400) não gasta o link. "
                + "Link desconhecido, já usado ou de conta que não pode entrar (400 invalid) e link vencido (400 expired) pedem um novo link. "
                + "A redefinição desbloqueia a conta e encerra todas as sessões abertas. "
                + "O mesmo IP tem um limite de pedidos por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.ResetPassword)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new ForgotPasswordCommand(request.Email ?? string.Empty), cancellationToken);

        return result.IsSuccess ? Results.Accepted() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(
            new ResetPasswordCommand(request.Token ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }
}