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

        finance.MapPost("/payments/{paymentId:guid}/receipt-deliveries", SendByEmailAsync)
            .WithName("SendReceiptByEmail")
            .WithSummary("Envia por e-mail o recibo de um pagamento")
            .WithDescription(ReceiptDescription.Email)
            .Produces<ReceiptEmailResponse>(StatusCodes.Status202Accepted)
            .WithErrors(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

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

    private static async Task<IResult> SendByEmailAsync(Guid paymentId, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        return ApiResults.Accepted(
            await sender.Send(new QueueReceiptEmailCommand(paymentId), cancellationToken), response, recipient => new ReceiptEmailResponse(recipient.Name));
    }
}

/// <param name="RecipientName">O nome de quem vai receber o recibo (o responsável da unidade); o endereço de e-mail não sai da API.</param>
public sealed record ReceiptEmailResponse(string RecipientName);

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

    public const string Email =
        "Sem corpo. Põe na fila o envio do recibo em PDF, como anexo, ao responsável da unidade do pagamento (inquilino antes de proprietário; dependente nunca); o e-mail sai em segundo plano e a resposta 202 diz só o nome de quem vai receber (recipientName), nunca o endereço. "
        + "Não há histórico nem trava: o síndico pode pedir de novo quantas vezes quiser (reenvio), e cada pedido manda um e-mail. Nada é enviado sozinho ao registrar um pagamento. "
        + "Pagamento de outro condomínio ou que não existe responde 404 finance.payment.not_found; pagamento estornado, 409 finance.receipt_delivery.payment_reversed (o recibo dele fica cancelado); unidade sem responsável com e-mail, 422 finance.receipt_delivery.no_recipient. "
        + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public const string Resident =
        "Devolve o mesmo PDF que o síndico baixa, só que do recibo de um pagamento de uma unidade em que a conta mora hoje como proprietária ou inquilina. "
        + "Pagamento de outra unidade, de outro condomínio, de dependente sem unidade própria ou que não existe responde 404, sempre igual, para o pedido não servir para descobrir pagamentos alheios. "
        + "O arquivo é montado a cada pedido e nunca é guardado; a resposta não vai para cache. O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";
}
