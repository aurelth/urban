using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Condominium.Application.PostalCodes;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Api.Endpoints;

public static class PostalCodeEndpoints
{
    public static IEndpointRouteBuilder MapPostalCodeEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder postalCodes = app.MapGroup(PostalCodeRoutes.Prefix).WithTags("CEP");

        postalCodes.MapGet("/{postalCode}", LookupAsync)
            .RequireAuthorization()
            .WithName("LookupPostalCode")
            .WithSummary("Consulta o endereço de um CEP")
            .WithDescription(
                "Exige o token de acesso de qualquer usuário logado, para a API não servir de consulta aberta a terceiros. "
                + "O CEP vai na rota, com ou sem hífen ou ponto. A API consulta a BrasilAPI e, se ela não achar ou estiver fora do ar, "
                + "o ViaCEP; o navegador nunca fala com eles. A resposta fica em cache (7 dias se achou, 1 hora se não existe). "
                + "Devolve 200 com CEP (só dígitos), rua, bairro, cidade e UF; rua e bairro podem vir vazios em CEPs gerais de cidades pequenas. "
                + "CEP inválido responde 400, CEP que não existe responde 404 e provedores fora do ar respondem 503: nos dois últimos, "
                + "a tela deixa o usuário preencher o endereço à mão. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.PostalCodeLookup)
            .Produces<PostalCodeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> LookupAsync(string postalCode, ISender sender, CancellationToken cancellationToken)
    {
        Result<PostalCodeAddress> result = await sender.Send(new LookupPostalCodeQuery(postalCode), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(CondominiumMapper.ToResponse(result.Value))
            : ErrorHttp.ToProblem(result.Error);
    }
}
