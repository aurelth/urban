using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Finance.Application.Delinquency;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>A inadimplência: as unidades com cobrança vencida e não paga. Só o síndico acessa.</summary>
public static class DelinquencyEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapDelinquencyEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder finance = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Finance);

        finance.MapGet("/delinquency", ListAsync)
            .WithName("ListDelinquency")
            .WithSummary("Lista as unidades com cobrança em atraso")
            .WithDescription(
                "Reúne as cobranças de todos os meses fechados que já venceram (o dia do vencimento ainda vale) e não foram pagas por inteiro, e as agrupa por unidade. "
                + "Cada unidade traz o que deve (balance), quantas cobranças estão em atraso, o vencimento mais antigo, os dias de atraso dele e as cobranças, da mais antiga para a mais nova, cada uma com valor, pago, saldo e dias de atraso. "
                + "A ordem é da unidade que mais deve para a que menos deve, e a das unidades do condomínio nos empates. totalOverdue e units valem para todas as unidades devedoras, não só para a página. "
                + "Filtros: page (a partir de 1) e pageSize (1 a 200, padrão 50); fora disso responde 400. A situação sai dos pagamentos a cada leitura: um pagamento estornado volta a contar como dívida. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<DelinquencyResponse>()
            .WithErrors(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> ListAsync(
        int? page, int? pageSize, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<DelinquencyView> result = await sender.Send(new ListDelinquencyQuery(page ?? 1, pageSize ?? 50), cancellationToken);

        return ApiResults.Respond(result, response, DelinquencyMapper.ToResponse);
    }
}
