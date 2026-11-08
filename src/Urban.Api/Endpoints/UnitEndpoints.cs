using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class UnitEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapUnitEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder units = app.MapGroup(UnitRoutes.Prefix).WithTags("Unidades").RequireAuthorization();

        units.MapGet("/", ListAsync)
            .WithName("ListUnits")
            .WithSummary("Lista as unidades do condomínio")
            .WithDescription(
                "Exige o token de acesso de um síndico com condomínio cadastrado. Devolve uma página na ordem natural (primeiro as sem bloco; "
                + "depois bloco e número, com 2 antes de 10). Filtros: status (active, deactivated ou all; o padrão é active), type (apartment ou house), "
                + "block (nome exato, sem diferenciar maiúsculas), search (trecho do número ou do bloco), page (a partir de 1) e pageSize (de 1 a 200; o padrão é 50). "
                + "Página ou tamanho fora do limite respondem 400. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<UnitListResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        units.MapGet("/summary", SummaryAsync)
            .WithName("GetUnitsSummary")
            .WithSummary("Resume as unidades e diz se cada método de rateio já pode ser usado")
            .WithDescription(
                "Devolve as contagens (ativas, desativadas, apartamentos e casas), a soma das frações ideais das unidades ativas (de 0 a 1), "
                + "quantas unidades ativas estão sem fração e sem área, o método de rateio escolhido com a versão dele e, para cada método, se os dados já bastam. "
                + "A fração ideal fica pronta quando toda unidade ativa tem fração e a soma fecha em 100% (com margem de 0,01 ponto percentual para o arredondamento dos coeficientes); "
                + "valor igual, quando há ao menos uma unidade ativa; área, quando toda unidade ativa tem área. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<UnitsSummaryResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        units.MapGet("/{id:guid}", GetAsync)
            .WithName("GetUnit")
            .WithSummary("Lê uma unidade")
            .WithDescription(
                "Devolve a unidade com a versão dela, que a edição precisa devolver. Uma unidade de outro condomínio responde 404, igual a uma que não existe, "
                + "para não revelar que ela existe. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<UnitResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        units.MapPost("/", CreateAsync)
            .WithName("CreateUnit")
            .WithSummary("Cadastra uma unidade")
            .WithDescription(
                "Bloco (opcional, até 30 caracteres), número (até 20), tipo (apartment ou house), fração ideal (de 0 a 1, até 8 casas) e área em m² (até 2 casas) opcionais. "
                + "O condomínio é o do token. Bloco e número, juntos, não se repetem no condomínio (sem diferenciar maiúsculas, contando as desativadas): 409. "
                + "A soma das frações das unidades ativas não passa de 100%: 422. Dado inválido responde 400 com o código do campo. "
                + "Devolve 201 com a unidade. O registro da alteração guarda só os nomes dos campos. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<UnitResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        units.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateUnit")
            .WithSummary("Edita uma unidade")
            .WithDescription(
                "O corpo leva a versão lida no GET: se outra pessoa mudou a unidade no meio do caminho, responde 409 e nada é gravado; sem a versão, 400. "
                + "Unidade desativada não se edita (409): reative primeiro. Se nada mudou, responde 200 sem gravar e sem trocar a versão. "
                + "As mesmas regras do cadastro valem (400, 409 e 422). O registro da alteração guarda só os nomes dos campos que mudaram. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<UnitResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        units.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateUnit")
            .WithSummary("Desativa uma unidade")
            .WithDescription(
                "A unidade nunca é apagada, porque morador e cobrança vão apontar para ela. Desativada, deixa de entrar na soma das frações e no rateio, "
                + "mas o bloco e o número continuam reservados. O corpo leva a versão (400 sem ela, 409 se mudou). Já desativada: 409. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<UnitResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        units.MapPost("/{id:guid}/reactivate", ReactivateAsync)
            .WithName("ReactivateUnit")
            .WithSummary("Reativa uma unidade")
            .WithDescription(
                "A fração da unidade volta para a soma, que não pode passar de 100% (422). O corpo leva a versão (400 sem ela, 409 se mudou). Já ativa: 409. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<UnitResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        units.MapPost("/batch", BatchAsync)
            .WithName("CreateUnitsBatch")
            .WithSummary("Cadastra várias unidades de uma vez")
            .WithDescription(
                "De 1 a 500 unidades, tudo ou nada: se uma falhar, nenhuma é gravada. Quando há problemas por linha, a resposta tem o status do primeiro erro, "
                + "o código dele e a lista errors, com a posição (a partir de 0, na ordem enviada) e o código de cada problema, para marcar as linhas. "
                + "Identificador repetido dentro do lote marca todas as posições envolvidas. Lote vazio ou grande demais: 400. Soma das frações acima de 100%: 422. "
                + "Devolve 201 com a quantidade criada e um único registro de alteração. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.UnitBatch)
            .Produces<UnitBatchCreatedResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        units.MapPut("/apportionment-method", ChangeMethodAsync)
            .WithName("ChangeApportionmentMethod")
            .WithSummary("Escolhe como as despesas são divididas entre as unidades")
            .WithDescription(
                "O método é idealFraction (o padrão), equalShare ou area. Só a escolha é gravada; o cálculo das cobranças vem em outro módulo. "
                + "O corpo leva a versão lida em GET /api/units/summary (400 sem ela, 409 se mudou). Método desconhecido: 400. "
                + "Escolher o método que já está valendo responde 200 sem gravar. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Units)
            .Produces<ApportionmentMethodResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        string? status,
        string? type,
        string? block,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        if (!UnitMapper.TryParseStatus(status, out UnitStatusFilter statusFilter))
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        UnitType? typeFilter = UnitMapper.ParseTypeFilter(type);
        if (typeFilter == 0)
        {
            return ErrorHttp.ToProblem(UnitErrors.TypeInvalid);
        }

        ListUnitsQuery query = new(block, typeFilter, statusFilter, search, page ?? 1, pageSize ?? 50);
        Result<UnitListing> result = await sender.Send(query, cancellationToken);

        return ApiResults.Respond(result, response, listing => UnitMapper.ToResponse(listing));
    }

    private static async Task<IResult> SummaryAsync(ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<UnitsSummary> result = await sender.Send(new GetUnitsSummaryQuery(), cancellationToken);

        return ApiResults.Respond(result, response, summary => UnitMapper.ToResponse(summary));
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<UnitView> result = await sender.Send(new GetUnitQuery(id), cancellationToken);

        return ApiResults.Respond(result, response, view => UnitMapper.ToResponse(view));
    }

    private static async Task<IResult> CreateAsync(UnitRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<UnitView> result = await sender.Send(UnitMapper.ToCreateCommand(request, userId), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Created($"{UnitRoutes.Prefix}/{result.Value.Id}", UnitMapper.ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(Guid id, UnitRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<UnitView> result = await sender.Send(UnitMapper.ToUpdateCommand(request, id, userId), cancellationToken);

        return ApiResults.Respond(result, response, view => UnitMapper.ToResponse(view));
    }

    private static async Task<IResult> DeactivateAsync(Guid id, UnitVersionRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<UnitView> result = await sender.Send(new DeactivateUnitCommand(userId, id, request.Version), cancellationToken);

        return ApiResults.Respond(result, response, view => UnitMapper.ToResponse(view));
    }

    private static async Task<IResult> ReactivateAsync(Guid id, UnitVersionRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<UnitView> result = await sender.Send(new ReactivateUnitCommand(userId, id, request.Version), cancellationToken);

        return ApiResults.Respond(result, response, view => UnitMapper.ToResponse(view));
    }

    private static async Task<IResult> BatchAsync(UnitBatchRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        UnitBatchResult result = await sender.Send(UnitMapper.ToBatchCommand(request, userId), cancellationToken);
        if (!result.IsSuccess)
        {
            IReadOnlyList<UnitBatchIssueResponse> issues = UnitMapper.ToIssues(result);

            return ErrorHttp.ToProblem(
                result.Rejection!,
                issues.Count == 0 ? null : new Dictionary<string, object?> { ["errors"] = issues });
        }

        response.PreventCaching();

        return Results.Created(UnitRoutes.Prefix, UnitMapper.ToCreatedResponse(result));
    }

    private static async Task<IResult> ChangeMethodAsync(ApportionmentMethodRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<ApportionmentView> result = await sender.Send(UnitMapper.ToCommand(request, userId), cancellationToken);

        return ApiResults.Respond(result, response, view => UnitMapper.ToResponse(view));
    }
}
