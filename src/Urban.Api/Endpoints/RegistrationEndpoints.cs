using MediatR;
using Urban.Api.Http;
using Urban.Identity.Application.Registration.CheckEmailLink;
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
            .WithSummary("Pede o cadastro da síndica")
            .WithDescription(
                "Recebe e-mail, nome e nome do condomínio e registra um pedido que aguarda a aprovação do administrador. "
                + "Nada é enviado à pessoa até a aprovação; só então ela recebe o link de confirmação (válido por 24 horas) e escolhe a senha. "
                + "Responde 202 para qualquer e-mail válido, esteja ele livre, em análise, recusado ou já cadastrado, para não revelar quem tem conta nem o resultado de um pedido. "
                + "E-mail, nome ou nome do condomínio inválidos respondem 400. "
                + "O mesmo IP tem um limite de pedidos por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Register)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        registration.MapPost("/confirm-email/check", CheckEmailLinkAsync)
            .WithName("CheckEmailLink")
            .WithSummary("Confere o link de confirmação sem gastá-lo")
            .WithDescription(
                "Recebe o token do link e diz se ele ainda serve (204), para a tela avisar de um link vencido ou já usado assim que abre, antes de a pessoa digitar a senha. "
                + "Nada é gravado e o link não é gasto: quem decide de verdade é a confirmação, que confere tudo de novo. "
                + "Link desconhecido, já usado, ausente ou de conta que não está pendente: 400 identity.email_verification.invalid; link vencido: 400 identity.email_verification.expired. "
                + "Usa o mesmo limite de requisições da confirmação; ao exceder, recebe 429 com Retry-After. A resposta nunca é guardada em cache.")
            .RequireRateLimiting(RateLimitPolicies.ConfirmEmail)
            .Produces(StatusCodes.Status204NoContent)
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
        Result result = await sender.Send(
            new RegisterManagerCommand(request.Email ?? string.Empty, request.Name ?? string.Empty, request.CondominiumName ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.Accepted() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> CheckEmailLinkAsync(
        CheckEmailLinkRequest request,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        response.PreventCaching();
        Result result = await sender.Send(new CheckEmailLinkQuery(request.Token ?? string.Empty), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
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