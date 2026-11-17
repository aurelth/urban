using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Finance.Application.Invoices;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>O envio das faturas por e-mail: pedir o envio do mês, reenviar uma unidade e ver como está cada uma. Só o síndico acessa.</summary>
public static class InvoiceDeliveryEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapInvoiceDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder reading = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Finance);

        reading.MapGet("/months/{month}/invoice-deliveries", ListAsync)
            .WithName("ListInvoiceDeliveries")
            .WithSummary("Mostra como está o envio das faturas de um mês fechado")
            .WithDescription(
                "O mês vai como AAAA-MM (400 se não for) e precisa estar fechado (409). Devolve uma linha por unidade, na ordem das unidades, com o responsável de hoje (inquilino antes de proprietário), a situação do envio (noRecipient, notSent, queued, sent ou failed), quando saiu pela última vez e, nas que falharam, o motivo. "
                + "noRecipient é a unidade sem responsável com e-mail cadastrado: o síndico precisa resolver o cadastro. counts soma cada situação no mês inteiro e total conta as unidades, para a paginação (page a partir de 1 e pageSize de 1 a 200, padrão 50). A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<InvoiceDeliveriesResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);

        RouteGroupBuilder sending = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Invoice);

        sending.MapPost("/months/{month}/invoice-deliveries", SendMonthAsync)
            .WithName("SendMonthInvoices")
            .WithSummary("Envia por e-mail a fatura de cada unidade do mês")
            .WithDescription(
                "Sem corpo. O mês vai como AAAA-MM e precisa estar fechado (409). Cada fatura em PDF vai em anexo ao e-mail do responsável da unidade (inquilino antes de proprietário; dependente nunca), em segundo plano e sem pressa: a resposta 202 só diz quantas entraram na fila (queued) e quantas ficaram de fora, por já terem sido enviadas (alreadySent), já estarem na fila (alreadyQueued) ou não terem responsável com e-mail (withoutRecipient). "
                + "Quem falhou entra de novo; pedir outra vez não repete o envio de quem já recebeu. O andamento se vê na listagem. " + RateLimitText)
            .Produces<SendMonthInvoicesResponse>(StatusCodes.Status202Accepted)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);

        sending.MapPost("/charges/{chargeId:guid}/invoice-deliveries", ResendAsync)
            .WithName("ResendInvoice")
            .WithSummary("Reenvia por e-mail a fatura de uma unidade")
            .WithDescription(
                "Sem corpo. Vale também para quem já recebeu (reenvio explícito). Responde 202 e o envio segue em segundo plano. Cobrança de outro condomínio responde 404; unidade sem responsável com e-mail, 422 finance.invoice_delivery.no_recipient; envio que já está na fila, 409 finance.invoice_delivery.already_queued. " + RateLimitText)
            .Produces(StatusCodes.Status202Accepted)
            .WithErrors(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> ListAsync(
        string month, int? page, int? pageSize, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<InvoiceDeliveriesView> result = await sender.Send(new ListInvoiceDeliveriesQuery(month, page ?? 1, pageSize ?? 50), cancellationToken);

        return ApiResults.Respond(result, response, InvoiceDeliveryMapper.ToResponse);
    }

    private static async Task<IResult> SendMonthAsync(string month, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<SendMonthInvoicesResult> result = await sender.Send(new SendMonthInvoicesCommand(month, userId), cancellationToken);

        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Accepted(value: InvoiceDeliveryMapper.ToResponse(result.Value));
    }

    private static async Task<IResult> ResendAsync(Guid chargeId, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        return ApiResults.Accepted(await sender.Send(new ResendInvoiceCommand(chargeId, userId), cancellationToken), response);
    }
}
