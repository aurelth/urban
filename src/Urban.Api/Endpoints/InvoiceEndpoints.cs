using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Finance.Application.Invoices;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>A fatura em PDF de uma cobrança. Só o síndico acessa.</summary>
public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder finance = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Invoice);

        finance.MapGet("/charges/{chargeId:guid}/invoice", DownloadAsync)
            .WithName("DownloadInvoice")
            .WithSummary("Baixa a fatura em PDF de uma cobrança")
            .WithDescription(
                "Devolve o PDF (application/pdf) da fatura de uma unidade de um mês fechado: o pedido de pagamento, com o condomínio, a unidade, a participação dela no rateio, o responsável, o contato do síndico, a competência, o vencimento, as linhas e o total, o demonstrativo dos gastos do mês despesa por despesa (por grupo, com subtotais e o fundo de reserva) e como pagar (Pix e conta, quando cadastrados). "
                + "O arquivo é montado a cada pedido e nunca é guardado; a resposta não vai para cache, e o nome sugerido é fatura-AAAA-MM-<unidade>.pdf. A fatura não traz nada de pagamento feito (situação, valor pago ou saldo): sai igual antes e depois de pagar, e isso é assunto do recibo. Cobrança de outro condomínio responde 404. O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .WithErrors(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> DownloadAsync(Guid chargeId, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<InvoiceFile> result = await sender.Send(new GetInvoiceQuery(chargeId), cancellationToken);

        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.File(result.Value.Content, "application/pdf", result.Value.FileName);
    }
}
