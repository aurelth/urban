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
                + "Pedir de novo em menos de 1 minuto não envia outro e-mail.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        registration.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirma o e-mail e define a senha")
            .WithDescription(
                "Troca o token do link e a senha escolhida pela conta ativa (204). O link vale uma vez só. "
                + "A senha é validada antes do token: senha fraca (400) não gasta o link. "
                + "Link desconhecido, já usado ou de conta que não está pendente (400 invalid) e link vencido (400 expired) pedem novo cadastro.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

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
            new ConfirmEmailCommand(request.Token ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }
}