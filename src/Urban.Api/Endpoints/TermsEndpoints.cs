using Urban.Api.Http;
using Urban.Identity.Application.Abstractions;

namespace Urban.Api.Endpoints;

public static class TermsEndpoints
{
    public static IEndpointRouteBuilder MapTermsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder terms = app.MapGroup("/api/terms").WithTags("Termos");

        terms.MapGet("/current", GetCurrent)
            .WithName("CurrentTerms")
            .WithSummary("Informa a versão vigente dos termos de uso e da política de privacidade")
            .WithDescription(
                "Público. A tela de cadastro lê esta versão, mostra os textos correspondentes e a devolve em "
                + "termsVersion ao confirmar o e-mail; qualquer outra versão é recusada. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.Terms)
            .Produces<TermsResponse>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static IResult GetCurrent(ICurrentTerms terms, HttpResponse response)
    {
        response.PreventCaching();

        return Results.Ok(new TermsResponse(terms.Version));
    }
}