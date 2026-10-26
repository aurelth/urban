using MediatR;
using Urban.Api.Http;
using Urban.Identity.Application.Registration.ConfirmEmail;
using Urban.Identity.Application.Registration.RegisterManager;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class RegistrationEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder registration = app.MapGroup(AuthRoutes.Prefix).WithTags("Cadastro");

        registration.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Cadastra a síndica com o e-mail")
            .WithDescription(
                "Recebe só o e-mail e envia o link de confirmação (válido por 24 horas). A senha é escolhida na confirmação. "
                + "Responde 202 para qualquer e-mail válido, esteja ele livre ou já cadastrado, para não revelar quem tem conta. "
                + "Pedir de novo em menos de 1 minuto não envia outro e-mail. "
                + "O mesmo IP tem um limite de pedidos por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Register)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        registration.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirma o e-mail, aceita os termos e define a senha")
            .WithDescription(
                "Troca o token do link, a senha escolhida e o aceite dos termos (termsVersion) pela conta ativa (204). O link vale uma vez só. "
                + "A senha e o aceite são validados antes do token: senha fraca, aceite ausente (identity.consent.required) "
                + "ou de versão que não é a vigente (identity.consent.outdated) respondem 400 e não gastam o link. "
                + "Link desconhecido, já usado ou de conta que não está pendente (400 invalid) e link vencido (400 expired) pedem novo cadastro. "
                + "O mesmo IP tem um limite de pedidos por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.ConfirmEmail)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RegisterManagerCommand(request.Email ?? string.Empty), cancellationToken);

        return result.IsSuccess ? Results.Accepted() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(
            new ConfirmEmailCommand(
                request.Token ?? string.Empty,
                request.Password ?? string.Empty,
                request.TermsVersion ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }
}