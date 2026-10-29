using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class CondominiumEndpoints
{
    public static IEndpointRouteBuilder MapCondominiumEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder condominiums = app.MapGroup(CondominiumRoutes.Prefix).WithTags("Condomínio");

        condominiums.MapPost("/", RegisterAsync)
            .RequireAuthorization()
            .WithName("RegisterCondominium")
            .WithSummary("Cadastra o condomínio e os dados do síndico")
            .WithDescription(
                "Exige o token de acesso de um síndico que ainda não cadastrou condomínio. Grava, numa transação só, o condomínio, "
                + "os dados pessoais do síndico (CPF cifrado) e o vínculo da conta, com o nome pelo qual ele prefere ser chamado. "
                + "Documentos, CEP e telefone vão só com dígitos; a data de nascimento vai como AAAA-MM-DD. "
                + "Devolve 201 com o identificador do condomínio, o nome, o CNPJ e o CPF mascarado, e nada mais. "
                + "Depois do cadastro, o token de acesso em uso não traz o condomínio: renove a sessão (refresh) para receber um novo. "
                + "Dado inválido responde 400 com o código do campo. Quem não é síndico recebe 403. "
                + "CNPJ já cadastrado (peça um convite ao síndico atual) e síndico que já cadastrou um condomínio respondem 409. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.CondominiumRegistration)
            .Produces<RegisteredCondominiumResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterCondominiumRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        // O papel vem do token assinado; o Identity confere de novo, na conta, dentro da transação.
        if (user.FindFirstValue(AccessTokenClaims.Role) != nameof(UserRole.Manager))
        {
            return ErrorHttp.ToProblem(UserErrors.NotManager);
        }

        Result<RegisteredCondominium> result = await sender.Send(CondominiumMapper.ToCommand(request, session.UserId), cancellationToken);

        return result.IsSuccess
            ? Results.Json(CondominiumMapper.ToResponse(result.Value), statusCode: StatusCodes.Status201Created)
            : ErrorHttp.ToProblem(result.Error);
    }
}
