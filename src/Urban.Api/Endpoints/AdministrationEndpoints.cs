using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Identity.Application.Administration;
using Urban.Identity.Application.Administration.GetPlatformSummary;
using Urban.Identity.Application.Administration.ListManagers;
using Urban.Identity.Application.Administration.ReactivateManager;
using Urban.Identity.Application.Administration.SuspendManager;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class AdministrationEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder admin = app.MapGroup(AdminRoutes.Prefix).WithTags("Administração");

        admin.MapGet("/summary", SummaryAsync)
            .RequireAuthorization()
            .WithName("GetPlatformSummary")
            .WithSummary("Resume a plataforma em números")
            .WithDescription(
                "Só o SuperAdmin ativo (403 para os demais). Devolve quantos síndicos estão ativos, suspensos e aguardando a confirmação do e-mail, "
                + $"quantos pedidos de cadastro esperam decisão e quantos condomínios já têm síndico. A resposta nunca é guardada em cache. {RateLimitText}")
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces<PlatformSummaryResponse>()
            .WithErrors();

        admin.MapGet("/managers", ListAsync)
            .RequireAuthorization()
            .WithName("ListManagers")
            .WithSummary("Lista os síndicos")
            .WithDescription(
                "Só o SuperAdmin ativo. Mostra as contas de síndico, das mais novas para as mais antigas, com o nome real do condomínio. "
                + "Filtros: status (active, suspended, deactivated ou pendingVerification), search (trecho do e-mail, do nome ou do condomínio), "
                + "page (a partir de 1) e pageSize (de 1 a 100; o padrão é 20). Pedidos em análise e recusados não aparecem aqui: têm tela própria. "
                + $"A resposta nunca é guardada em cache. {RateLimitText}")
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces<ManagerListResponse>()
            .WithErrors(StatusCodes.Status400BadRequest);

        admin.MapPost("/managers/{id:guid}/suspend", SuspendAsync)
            .RequireAuthorization()
            .WithName("SuspendManager")
            .WithSummary("Suspende um síndico")
            .WithDescription(
                "Só o SuperAdmin ativo. O motivo é obrigatório (até 500 caracteres), fica registrado e aparece na lista. "
                + "Todas as sessões do síndico terminam na hora e ele não consegue entrar até ser reativado (204). "
                + "Conta que não existe ou não é de síndico responde 404; conta que não pode ser suspensa no estado atual, 409. "
                + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        admin.MapPost("/managers/{id:guid}/reactivate", ReactivateAsync)
            .RequireAuthorization()
            .WithName("ReactivateManager")
            .WithSummary("Reativa um síndico suspenso")
            .WithDescription(
                "Só o SuperAdmin ativo. Tira a suspensão e apaga o motivo; o síndico entra de novo com a mesma senha (204). "
                + "Conta que não existe ou não é de síndico responde 404; conta que não está suspensa, 409. "
                + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> SummaryAsync(ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result<PlatformSummary> result = await sender.Send(new GetPlatformSummaryQuery(session.UserId), cancellationToken);

        return ApiResults.Respond(result, response, PlatformSummaryResponse.From);
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        string? status,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        if (!TryParseStatus(status, out AccountStatus? statusFilter))
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        ListManagersQuery query = new(session.UserId, statusFilter, search, page ?? 1, pageSize ?? 20);
        Result<ManagerListing> result = await sender.Send(query, cancellationToken);

        return ApiResults.Respond(result, response, ManagerListResponse.From);
    }

    private static async Task<IResult> SuspendAsync(
        Guid id,
        SuspendManagerRequest? request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result result = await sender.Send(new SuspendManagerCommand(session.UserId, id, request?.Reason), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> ReactivateAsync(Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result result = await sender.Send(new ReactivateManagerCommand(session.UserId, id), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }

    /// <summary>Texto vazio é "sem filtro"; um nome de estado que não existe é erro de quem chamou. Quais estados a lista cobre é decisão do Application.</summary>
    private static bool TryParseStatus(string? text, out AccountStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!Enum.TryParse(text.Trim(), ignoreCase: true, out AccountStatus parsed) || !Enum.IsDefined(parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}
