using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Condominium.Application.Profile;
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

        condominiums.MapGet("/mine", GetMineAsync)
            .RequireAuthorization()
            .WithName("GetMyCondominium")
            .WithSummary("Lê o cadastro do síndico logado")
            .WithDescription(
                "Exige o token de acesso de um síndico que já cadastrou o condomínio. Devolve o nome de exibição, os dados do síndico "
                + "(com o CPF mascarado), os do condomínio e a versão dos dados, que a edição precisa devolver. "
                + "O CPF inteiro nunca sai da API. A resposta nunca é guardada em cache. "
                + "Quem não é síndico recebe 403; síndico sem condomínio cadastrado recebe 404. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.CondominiumProfile)
            .Produces<MyCondominiumResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        condominiums.MapPut("/mine", UpdateMineAsync)
            .RequireAuthorization()
            .WithName("UpdateMyCondominium")
            .WithSummary("Edita os campos livres do cadastro")
            .WithDescription(
                "Muda o nome de exibição, o celular, o endereço do síndico e o nome e o endereço do condomínio. O CNPJ, o CPF, o nome completo "
                + "e a data de nascimento não mudam por aqui (CNPJ nunca muda; os outros passam por manager-identity, com a senha). "
                + "O corpo leva a versão lida no GET: se outra pessoa mudou os dados no meio do caminho, responde 409 e nada é gravado. "
                + "Sem a versão, 400. Dado inválido responde 400 com o código do campo. Devolve os dados já salvos, com a nova versão. "
                + "Cada edição deixa um registro com os nomes dos campos que mudaram, nunca os valores. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.CondominiumProfile)
            .Produces<MyCondominiumResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        condominiums.MapPut("/mine/manager-identity", UpdateIdentityAsync)
            .RequireAuthorization()
            .WithName("UpdateManagerIdentity")
            .WithSummary("Edita o nome completo, a data de nascimento e o CPF, com a senha")
            .WithDescription(
                "Exige a senha da conta no corpo. Os campos são validados antes da senha (campo inválido não gasta tentativa); senha errada "
                + "responde 403 e conta para o bloqueio da conta (cinco erros bloqueiam por 15 minutos, 423). O CPF ausente mantém o atual; "
                + "CPF que já é de outra pessoa do condomínio responde 409. O corpo leva a versão lida no GET (409 se mudou). "
                + "Devolve os dados já salvos, com o CPF mascarado e a nova versão. O registro da edição guarda só os nomes dos campos. "
                + "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.")
            .RequireRateLimiting(RateLimitPolicies.SensitiveChange)
            .Produces<MyCondominiumResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!TryReadManager(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<MyCondominium> result = await sender.Send(new GetMyCondominiumQuery(userId), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Ok(CondominiumMapper.ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateMineAsync(
        UpdateMyCondominiumRequest request,
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!TryReadManager(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<MyCondominium> result = await sender.Send(CondominiumMapper.ToCommand(request, userId), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Ok(CondominiumMapper.ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateIdentityAsync(
        UpdateManagerIdentityRequest request,
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!TryReadManager(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<MyCondominium> updated = await sender.Send(CondominiumMapper.ToCommand(request, userId), cancellationToken);
        if (updated.IsFailure)
        {
            return ErrorHttp.ToProblem(updated.Error);
        }

        // O comando de identidade não conhece o nome de exibição (mora na conta); a leitura devolve o cadastro completo.
        Result<MyCondominium> complete = await sender.Send(new GetMyCondominiumQuery(userId), cancellationToken);
        if (complete.IsFailure)
        {
            return ErrorHttp.ToProblem(complete.Error);
        }

        response.PreventCaching();

        return Results.Ok(CondominiumMapper.ToResponse(complete.Value));
    }

    /// <summary>O papel vem do token assinado; a conta e o cadastro são conferidos de novo pelos comandos.</summary>
    private static bool TryReadManager(ClaimsPrincipal user, out Guid userId, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out IResult? refusal)
    {
        userId = Guid.Empty;
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            refusal = ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
            return false;
        }

        if (user.FindFirstValue(AccessTokenClaims.Role) != nameof(UserRole.Manager))
        {
            refusal = ErrorHttp.ToProblem(UserErrors.NotManager);
            return false;
        }

        userId = session.UserId;
        refusal = null;
        return true;
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
