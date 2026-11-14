using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class ResidentEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapResidentEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder residents = app.MapGroup(ResidentRoutes.Prefix).WithTags("Moradores").RequireAuthorization();

        residents.MapGet("/", ListAsync)
            .WithName("ListResidents")
            .WithSummary("Lista os moradores do condomínio")
            .WithDescription(
                "Exige o token de acesso de um síndico com condomínio cadastrado. Morador é a pessoa com ao menos um vínculo com uma unidade. "
                + "Devolve uma página em ordem de nome, cada morador com todos os vínculos (bloco e número da unidade, tipo e datas). "
                + "Filtros: search (trecho do nome, sem diferenciar maiúsculas), unitId (só quem tem vínculo com a unidade), status (active: tem vínculo em andamento; former: já saiu e não tem nenhum em andamento; all, o padrão: todos; com unitId, o estado é o da pessoa naquela unidade; outro valor responde 400), page (a partir de 1) e pageSize (de 1 a 200; o padrão é 50). "
                + "Página ou tamanho fora do limite respondem 400. O CPF sai mascarado. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Residents)
            .Produces<ResidentListResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        residents.MapGet("/{id:guid}", GetAsync)
            .WithName("GetResident")
            .WithSummary("Lê um morador")
            .WithDescription(
                "Devolve o morador com os vínculos e a versão. Um morador de outro condomínio responde 404, igual a um que não existe, "
                + "para não revelar que ele existe. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Residents)
            .Produces<ResidentResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        residents.MapPost("/", CreateAsync)
            .WithName("RegisterResident")
            .WithSummary("Cadastra um morador adulto, com o vínculo com a unidade")
            .WithDescription(
                "Cadastra a pessoa e o primeiro vínculo (proprietário ou inquilino) numa gravação só. Cadastrar não cria conta nem envia e-mail: o acesso vem com o convite. "
                + "Nome completo, CPF, data de nascimento (18 anos ou mais), celular ou telefone fixo com DDD, profissão e contato de emergência (nome, parentesco e telefone) são obrigatórios; "
                + "o e-mail é opcional. A unidade tem de ser do condomínio e estar ativa; a data de entrada vai até 1 ano à frente. "
                + "O mesmo CPF não se repete no condomínio: 409. Dado inválido responde 400 com o código do campo (o primeiro que falhar). Devolve 201 com o morador. "
                + "O registro da alteração guarda só os nomes dos campos. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Residents)
            .Produces<ResidentResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        residents.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateResident")
            .WithSummary("Edita os dados de um morador")
            .WithDescription(
                "Substitui todos os dados pessoais do cadastro. O corpo leva a version que a tela leu: sem ela, 400; se outra pessoa editou antes, 409 com concurrency.conflict. "
                + "O CPF é opcional: a resposta só mostra o CPF mascarado, então em branco mantém o atual e preenchido troca (o CPF não pode ser o de outra pessoa do condomínio: 409). "
                + "O e-mail em branco limpa o e-mail. Se nada mudou, nada é gravado e a versão continua a mesma. Dado inválido responde 400 com o código do campo (o primeiro que falhar). "
                + "O registro da alteração guarda só os nomes dos campos. Um morador de outro condomínio responde 404. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Residents)
            .Produces<ResidentResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        residents.MapPost("/{id:guid}/occupancies", AddOccupancyAsync)
            .WithName("AddResidentOccupancy")
            .WithSummary("Liga o morador a mais uma unidade")
            .WithDescription(
                "Cria um novo vínculo (proprietário ou inquilino) de um morador que já existe com uma unidade ativa do condomínio. "
                + "Quem já mora na unidade não ganha um segundo vínculo em andamento (409); quem já saiu dela pode voltar. A data de entrada vai até 1 ano à frente. "
                + "Dado inválido responde 400 com o código do campo. Devolve 201 com o morador e todos os vínculos. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Residents)
            .Produces<ResidentResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        residents.MapPost("/{id:guid}/occupancies/{occupancyId:guid}/end", EndOccupancyAsync)
            .WithName("EndResidentOccupancy")
            .WithSummary("Registra a saída do morador de uma unidade")
            .WithDescription(
                "Preenche a data de saída do vínculo, que não é apagado: fica no histórico de quem morou na unidade. "
                + "A data vai da entrada até hoje (horário de Brasília); fora disso, 400 com condominium.occupancy.end_date_invalid. "
                + "Um vínculo que já terminou responde 409; o de outro morador responde 404. Devolve o morador com os vínculos. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Residents)
            .Produces<ResidentResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        string? search,
        Guid? unitId,
        string? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        if (!ResidentMapper.TryParseStatus(status, out ResidentStatusFilter statusFilter))
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        Result<ResidentListing> result = await sender.Send(new ListResidentsQuery(search, unitId, page ?? 1, pageSize ?? 50, statusFilter), cancellationToken);

        return ApiResults.Respond(result, response, listing => ResidentMapper.ToResponse(listing));
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<ResidentView> result = await sender.Send(new GetResidentQuery(id), cancellationToken);

        return ApiResults.Respond(result, response, view => ResidentMapper.ToResponse(view));
    }

    private static async Task<IResult> CreateAsync(ResidentRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<ResidentView> result = await sender.Send(ResidentMapper.ToCommand(request, userId), cancellationToken);

        return ApiResults.Created(result, response, view => $"{ResidentRoutes.Prefix}/{view.Id}", view => ResidentMapper.ToResponse(view));
    }

    private static async Task<IResult> UpdateAsync(Guid id, ResidentUpdateRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<ResidentView> result = await sender.Send(ResidentMapper.ToCommand(id, request, userId), cancellationToken);

        return ApiResults.Respond(result, response, view => ResidentMapper.ToResponse(view));
    }

    private static async Task<IResult> AddOccupancyAsync(Guid id, OccupancyRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<ResidentView> result = await sender.Send(ResidentMapper.ToCommand(id, request, userId), cancellationToken);

        return ApiResults.Created(result, response, view => $"{ResidentRoutes.Prefix}/{view.Id}", view => ResidentMapper.ToResponse(view));
    }

    private static async Task<IResult> EndOccupancyAsync(
        Guid id,
        Guid occupancyId,
        OccupancyEndRequest request,
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<ResidentView> result = await sender.Send(ResidentMapper.ToCommand(id, occupancyId, request, userId), cancellationToken);

        return ApiResults.Respond(result, response, view => ResidentMapper.ToResponse(view));
    }
}
