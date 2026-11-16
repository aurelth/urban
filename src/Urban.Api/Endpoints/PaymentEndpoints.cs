using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Finance.Application.Payments;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>O registro manual de pagamentos das cobranças de um mês fechado. Só o síndico acessa.</summary>
public static class PaymentEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder finance = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Finance);

        finance.MapGet("/months/{month}/payments", ListMonthAsync)
            .WithName("ListMonthPayments")
            .WithSummary("Lista as cobranças de um mês fechado com o que já foi pago")
            .WithDescription(
                "O mês vai como AAAA-MM (400 se não for) e precisa estar fechado (409). Devolve uma linha por cobrança, na ordem das unidades, com o valor, o pago, o saldo, a situação (open, partial, paid ou overdue, calculada a cada leitura) e a data do último recebimento. "
                + "charged e received são do mês inteiro, com ou sem filtro; total conta as cobranças do filtro. Filtros: status (open, partial, paid ou overdue; outro valor responde 400), page (a partir de 1) e pageSize (1 a 200, padrão 50). A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<MonthPaymentsResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        finance.MapGet("/charges/{chargeId:guid}/payments", ListChargeAsync)
            .WithName("ListChargePayments")
            .WithSummary("Lista os pagamentos de uma cobrança")
            .WithDescription("Do mais novo para o mais antigo, com os estornados também (reversedAt e reversalReason preenchidos). paid soma só os não estornados. Cobrança de outro condomínio responde 404. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<ChargePaymentsResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        finance.MapPost("/charges/{chargeId:guid}/payments", RegisterAsync)
            .WithName("RegisterPayment")
            .WithSummary("Registra um pagamento contra a cobrança de uma unidade")
            .WithDescription(
                "Corpo: amount (maior que zero, até 2 casas e R$ 9.999.999,99), receivedOn (AAAA-MM-DD, não futura nem anterior ao primeiro dia de 12 meses antes do mês da cobrança), method (pix, boleto, transfer, cash ou other) e note (opcional, uma linha de até 200 caracteres). "
                + "Pode haver vários pagamentos por cobrança, até o valor dela: passar do saldo responde 422 finance.payment.exceeds_balance, com o saldo em data.balance, e não existe crédito. Devolve 201 com o pagamento. " + RateLimitText)
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);

        finance.MapPost("/payments/{paymentId:guid}/reverse", ReverseAsync)
            .WithName("ReversePayment")
            .WithSummary("Estorna um pagamento")
            .WithDescription("Corpo: reason (obrigatório, uma linha de até 200 caracteres). O pagamento nunca é apagado: continua na lista, marcado, e deixa de contar no pago. O estorno é definitivo (409 se já estornado). Responde 204. Um mês com pagamentos, estornados ou não, não pode ser reaberto. " + RateLimitText)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListMonthAsync(
        string month, string? status, int? page, int? pageSize, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        if (!PaymentMapper.TryParseStatus(status, out ChargeStatus? filter))
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        Result<MonthPaymentsView> result = await sender.Send(new ListMonthPaymentsQuery(month, filter, page ?? 1, pageSize ?? 50), cancellationToken);

        return ApiResults.Respond(result, response, PaymentMapper.ToResponse);
    }

    private static async Task<IResult> ListChargeAsync(Guid chargeId, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        return ApiResults.Respond(await sender.Send(new ListChargePaymentsQuery(chargeId), cancellationToken), response, PaymentMapper.ToResponse);
    }

    private static async Task<IResult> RegisterAsync(
        Guid chargeId, RegisterPaymentRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<PaymentView> result = await sender.Send(PaymentMapper.ToCommand(request, chargeId, userId), cancellationToken);

        return ApiResults.Created(result, response, view => $"{FinanceRoutes.Prefix}/charges/{view.ChargeId}/payments", PaymentMapper.ToResponse);
    }

    private static async Task<IResult> ReverseAsync(
        Guid paymentId, ReversePaymentRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result result = await sender.Send(PaymentMapper.ToCommand(request, paymentId, userId), cancellationToken);

        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.NoContent();
    }
}
