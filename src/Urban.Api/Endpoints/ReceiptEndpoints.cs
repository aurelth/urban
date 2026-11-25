using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Finance.Application.Receipts;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>O recibo em PDF de um pagamento. O síndico baixa o de qualquer pagamento; o do morador está em <c>MyFinanceEndpoints</c>.</summary>
public static class ReceiptEndpoints
{
    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder finance = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Invoice);

        finance.MapGet("/payments/{paymentId:guid}/receipt", DownloadAsync)
            .WithName("DownloadReceipt")
            .WithSummary("Baixa o recibo em PDF de um pagamento")
            .WithDescription(ReceiptDescription.Manager)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .WithErrors(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> DownloadAsync(Guid paymentId, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        return ReceiptFiles.ToResult(await sender.Send(new GetReceiptQuery(paymentId), cancellationToken), response);
    }
}

/// <summary>A resposta de um recibo, a mesma para o síndico e para o morador: o PDF sem cache, ou o problema.</summary>
internal static class ReceiptFiles
{
    public static IResult ToResult(Result<ReceiptFile> result, HttpResponse response)
    {
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.File(result.Value.Content, "application/pdf", result.Value.FileName);
    }
}

internal static class ReceiptDescription
{
    public const string Manager =
        "Devolve o PDF (application/pdf) do recibo de um pagamento: o condomínio, a unidade, o responsável, a competência da fatura paga, o valor recebido, a data e a forma do recebimento, o total pago da fatura até aquele pagamento e o saldo restante (ou \"Fatura quitada\"). "
        + "O número do recibo (REC-000123) é o do pagamento, em sequência por condomínio. Pagamento estornado continua com recibo, marcado como CANCELADO e sem saldo; o motivo do estorno e a observação do pagamento nunca saem no PDF. "
        + "O arquivo é montado a cada pedido e nunca é guardado; a resposta não vai para cache, e o nome sugerido é recibo-NNNNNN.pdf. Pagamento de outro condomínio ou que não existe responde 404. "
        + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public const string Resident =
        "Devolve o mesmo PDF que o síndico baixa, só que do recibo de um pagamento de uma unidade em que a conta mora hoje como proprietária ou inquilina. "
        + "Pagamento de outra unidade, de outro condomínio, de dependente sem unidade própria ou que não existe responde 404, sempre igual, para o pedido não servir para descobrir pagamentos alheios. "
        + "O arquivo é montado a cada pedido e nunca é guardado; a resposta não vai para cache. O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";
}
