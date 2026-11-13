using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Application.RegistrationRequests.ApproveRegistration;
using Urban.Identity.Application.RegistrationRequests.ListRegistrationRequests;
using Urban.Identity.Application.RegistrationRequests.RejectRegistration;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class RegistrationRequestEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationRequestEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder requests = app.MapGroup($"{AdminRoutes.Prefix}/registration-requests").WithTags("Administração");

        requests.MapGet("/", ListAsync)
            .RequireAuthorization()
            .WithName("ListRegistrationRequests")
            .WithSummary("Lista os pedidos de cadastro em análise")
            .WithDescription(
                "Só o SuperAdmin ativo (403 para os demais). Devolve os pedidos que aguardam decisão, do mais antigo para o mais novo, "
                + "com e-mail, nome, nome do condomínio e quando chegaram. A resposta nunca é guardada em cache. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces<RegistrationRequestResponse[]>()
            .WithErrors();

        requests.MapPost("/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization()
            .WithName("ApproveRegistrationRequest")
            .WithSummary("Aprova um pedido de cadastro")
            .WithDescription(
                "Só o SuperAdmin ativo. Move o pedido para a confirmação de e-mail e envia à pessoa o link para confirmar e escolher a senha (204). "
                + "Pedido que não existe responde 404; pedido que já foi decidido, 409. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        requests.MapPost("/{id:guid}/reject", RejectAsync)
            .RequireAuthorization()
            .WithName("RejectRegistrationRequest")
            .WithSummary("Recusa um pedido de cadastro")
            .WithDescription(
                "Só o SuperAdmin ativo. A recusa é final e silenciosa: nada é enviado à pessoa, e novos pedidos com o mesmo e-mail continuam sendo ignorados (204). "
                + "Pedido que não existe responde 404; pedido que já foi decidido, 409. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result<IReadOnlyList<RegistrationRequestSummary>> result = await sender.Send(new ListRegistrationRequestsQuery(session.UserId), cancellationToken);

        return ApiResults.Respond(result, response, list => list.Select(RegistrationRequestResponse.From).ToArray());
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result result = await sender.Send(new ApproveRegistrationCommand(session.UserId, id), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result result = await sender.Send(new RejectRegistrationCommand(session.UserId, id), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }
}
