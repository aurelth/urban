using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Application.MyMonth;
using Urban.Finance.Application.Receipts;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>
/// O financeiro de quem mora no condomínio: a cobrança de cada unidade dele, as despesas do mês por grupo e a fatura em PDF da
/// própria unidade. Servem o morador (proprietário ou inquilino) e o síndico que também mora no condomínio.
/// </summary>
public static class MyFinanceEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapMyFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder me = app.MapGroup(FinanceRoutes.Prefix + "/me").WithTags("Financeiro do morador").RequireAuthorization();

        me.MapGet("/months/{month}", GetMonthAsync)
            .RequireRateLimiting(RateLimitPolicies.Finance)
            .WithName("GetMyMonth")
            .WithSummary("Lê o mês financeiro de quem mora no condomínio")
            .WithDescription(
                "O mês vai como AAAA-MM (400 se não for) e pode estar aberto, fechado ou nunca ter sido aberto: não gera nada e não responde 409. "
                + "Quem é lido vem do token, nunca da rota: units traz as unidades em que a conta mora hoje como proprietária ou inquilina, cada uma com a sua cobrança "
                + "(vencimento, valor, pago sem estornos, o que falta, situação open, partial, paid ou overdue e o dia do último pagamento), que é nula enquanto o mês não fechou. "
                + "expenses traz o total das despesas do mês e o de cada grupo, sem fornecedor, observação nem descrição dos lançamentos, e só vem em mês fechado. "
                + "access diz como a conta mora: unit, dependent (só como dependente: não há cobrança própria, só o aviso) ou none (sem unidade; é o caso do síndico que não mora no condomínio). "
                + "Nada do que o prédio recebeu nem de quem está atrasado aparece aqui. A situação é calculada a cada leitura, com o dia de hoje em Brasília (asOf). A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<MyMonthResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        me.MapGet("/access", GetAccessAsync)
            .RequireRateLimiting(RateLimitPolicies.Finance)
            .WithName("GetMyAccess")
            .WithSummary("Diz como a conta mora no condomínio hoje")
            .WithDescription(
                "Responde só access: unit (mora hoje como proprietária ou inquilina de ao menos uma unidade), dependent (só como dependente) ou none (sem unidade; é o caso do síndico que não mora no condomínio). "
                + "É a mesma regra do campo access do mês financeiro, sem ler mês nem cobrança: serve ao menu, que só mostra \"Minha fatura\" ao síndico que mora no condomínio. "
                + "A conta vem do token, nunca da rota, e a resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<MyAccessResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        me.MapGet("/charges/{chargeId:guid}/invoice", DownloadInvoiceAsync)
            .RequireRateLimiting(RateLimitPolicies.Invoice)
            .WithName("DownloadMyInvoice")
            .WithSummary("Baixa a fatura em PDF de uma cobrança da própria unidade")
            .WithDescription(
                "Devolve o mesmo PDF (application/pdf) que o síndico baixa, só que da cobrança de uma unidade em que a conta mora hoje como proprietária ou inquilina. "
                + "Cobrança de outra unidade, de outro condomínio, de dependente sem unidade própria ou que não existe responde 404, sempre igual, para o pedido não servir para descobrir cobranças alheias. "
                + "O arquivo é montado a cada pedido e nunca é guardado; a resposta não vai para cache. " + RateLimitText)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .WithErrors(StatusCodes.Status404NotFound);

        me.MapGet("/payments/{paymentId:guid}/receipt", DownloadReceiptAsync)
            .RequireRateLimiting(RateLimitPolicies.Invoice)
            .WithName("DownloadMyReceipt")
            .WithSummary("Baixa o recibo em PDF de um pagamento da própria unidade")
            .WithDescription(ReceiptDescription.Resident)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .WithErrors(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetMonthAsync(string month, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!MemberAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<MyMonthView> result = await sender.Send(new GetMyMonthQuery(userId, month), cancellationToken);

        return ApiResults.Respond(result, response, MyFinanceMapper.ToResponse);
    }

    private static async Task<IResult> GetAccessAsync(ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!MemberAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<MyMonthAccess> result = await sender.Send(new GetMyAccessQuery(userId), cancellationToken);

        return ApiResults.Respond(result, response, MyFinanceMapper.ToResponse);
    }

    private static async Task<IResult> DownloadInvoiceAsync(Guid chargeId, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!MemberAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<InvoiceFile> result = await sender.Send(new GetInvoiceQuery(chargeId, userId), cancellationToken);

        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.File(result.Value.Content, "application/pdf", result.Value.FileName);
    }

    private static async Task<IResult> DownloadReceiptAsync(Guid paymentId, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!MemberAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        return ReceiptFiles.ToResult(await sender.Send(new GetReceiptQuery(paymentId, userId), cancellationToken), response);
    }
}
