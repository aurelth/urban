using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Finance.Application.Instructions;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>Os dados para o morador pagar a fatura (chave Pix e conta bancária do condomínio). Só o síndico acessa.</summary>
public static class PaymentInstructionsEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapPaymentInstructionsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder finance = app.MapGroup(FinanceRoutes.Prefix).WithTags("Financeiro").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Finance);

        finance.MapGet("/payment-instructions", GetAsync)
            .WithName("GetPaymentInstructions")
            .WithSummary("Lê os dados para pagamento do condomínio")
            .WithDescription(
                "Devolve a chave Pix (pix) e a conta bancária (account), cada uma nula enquanto não cadastrada, e updatedAt (nulo enquanto o condomínio não cadastrou nada). A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<PaymentInstructionsResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        finance.MapPut("/payment-instructions", SaveAsync)
            .WithName("SavePaymentInstructions")
            .WithSummary("Cadastra os dados para pagamento do condomínio")
            .WithDescription(
                "Troca tudo de uma vez: o que não vier no corpo deixa de estar cadastrado, e um corpo vazio apaga os dados. pix: kind (cnpj, cpf, email, phone ou random) e key, que a API guarda no formato do banco. "
                + "account: bank (2 a 60 caracteres), agency (até 5 dígitos, com dígito verificador opcional depois de hífen), number (até 12 dígitos, com dígito verificador opcional) e kind (checking ou savings). "
                + "Vale o primeiro problema, começando pelo Pix; nada é gravado se houver um. Os dados aparecem na fatura em PDF. " + RateLimitText)
            .Produces<PaymentInstructionsResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<PaymentInstructionsView> result = await sender.Send(new GetPaymentInstructionsQuery(), cancellationToken);

        return ApiResults.Respond(result, response, PaymentInstructionsMapper.ToResponse);
    }

    private static async Task<IResult> SaveAsync(PaymentInstructionsRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<PaymentInstructionsView> result = await sender.Send(PaymentInstructionsMapper.ToCommand(request), cancellationToken);

        return ApiResults.Respond(result, response, PaymentInstructionsMapper.ToResponse);
    }
}
