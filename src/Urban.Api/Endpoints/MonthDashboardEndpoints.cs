using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Finance.Application.Dashboard;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>O painel financeiro de um mês: despesas por grupo e situação das cobranças. Só o síndico acessa.</summary>
public static class MonthDashboardEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapMonthDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder finance = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Finance);

        finance.MapGet("/months/{month}/dashboard", GetAsync)
            .WithName("GetMonthDashboard")
            .WithSummary("Lê o painel financeiro de um mês")
            .WithDescription(
                "O mês vai como AAAA-MM (400 se não for) e pode estar aberto, fechado ou nunca ter sido aberto: não gera nada e não responde 409. "
                + "expenses traz o total das despesas do mês (sem as canceladas), quantas esperam valor, quantas venceram e o total de cada grupo. "
                + "closed diz se o mês foi fechado; só então charges vem preenchido, com o vencimento, o cobrado, o recebido (sem estornos), o que falta e quantas unidades estão em aberto, parciais, pagas e atrasadas, "
                + "mais o saldo das cobranças atrasadas deste mês. A situação é calculada a cada leitura, com o dia de hoje em Brasília (asOf): o dia do vencimento ainda vale. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<MonthDashboardResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetAsync(string month, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<MonthDashboardView> result = await sender.Send(new GetMonthDashboardQuery(month), cancellationToken);

        return ApiResults.Respond(result, response, MonthDashboardMapper.ToResponse);
    }
}
