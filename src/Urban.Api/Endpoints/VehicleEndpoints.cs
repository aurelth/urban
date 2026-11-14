using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Condominium.Application.Vehicles;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class VehicleEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder vehicles = app.MapGroup(VehicleRoutes.Prefix).WithTags("Veículos").RequireAuthorization();

        vehicles.MapGet("/", ListAsync)
            .WithName("ListVehicles")
            .WithSummary("Lista os veículos do condomínio")
            .WithDescription(
                "Exige o token de acesso de um síndico com condomínio cadastrado. Devolve uma página na ordem da unidade (primeiro as sem bloco; depois bloco e número, com 2 antes de 10) e, dentro da unidade, da placa. "
                + "Filtros: unitId (só os veículos da unidade), search (trecho da placa, sem diferenciar maiúsculas nem hífen, ou do modelo ou da cor), page (a partir de 1) e pageSize (de 1 a 200; o padrão é 50). "
                + "Página ou tamanho fora do limite respondem 400. Cada veículo traz o bloco e o número da unidade. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Vehicles)
            .Produces<VehicleListResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        vehicles.MapGet("/{id:guid}", GetAsync)
            .WithName("GetVehicle")
            .WithSummary("Lê um veículo")
            .WithDescription(
                "Devolve o veículo com a versão dele, que a edição precisa devolver. Um veículo de outro condomínio responde 404, igual a um que não existe, "
                + "para não revelar que ele existe. A resposta nunca é guardada em cache. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Vehicles)
            .Produces<VehicleResponse>()
            .WithErrors(StatusCodes.Status404NotFound);

        vehicles.MapPost("/", RegisterAsync)
            .WithName("RegisterVehicle")
            .WithSummary("Cadastra um veículo")
            .WithDescription(
                "Unidade (do condomínio e ativa), placa (formato antigo ABC-1234 ou Mercosul ABC1D23, em qualquer caixa e com ou sem hífen), modelo (2 a 60 caracteres) e cor (2 a 30). "
                + "O condomínio é o do token. A placa é guardada normalizada e não se repete no condomínio: 409. Unidade inexistente ou de outro condomínio: 404; unidade desativada: 409. "
                + "Dado inválido responde 400 com o código do campo. Devolve 201 com o veículo. O registro da alteração guarda só os nomes dos campos. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Vehicles)
            .Produces<VehicleResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        vehicles.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateVehicle")
            .WithSummary("Edita um veículo")
            .WithDescription(
                "O corpo leva a versão lida no GET: se outra pessoa mudou o veículo no meio do caminho, responde 409 e nada é gravado; sem a versão, 400. "
                + "Trocar de unidade exige uma unidade ativa do condomínio (404 ou 409); editar o resto de um veículo de unidade desativada é permitido. "
                + "Se nada mudou, responde 200 sem gravar e sem trocar a versão. As mesmas regras do cadastro valem. O registro da alteração guarda só os nomes dos campos que mudaram. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Vehicles)
            .Produces<VehicleResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        vehicles.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteVehicle")
            .WithSummary("Exclui um veículo")
            .WithDescription(
                "Apaga o veículo e libera a placa. Nada mais depende de um veículo, por isso a exclusão é definitiva (a unidade, ao contrário, só se desativa). "
                + "Um veículo de outro condomínio ou que já foi apagado responde 404. Devolve 204. O registro da alteração guarda só que o veículo foi removido, sem nenhum valor. " + RateLimitText)
            .RequireRateLimiting(RateLimitPolicies.Vehicles)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        Guid? unitId,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<VehicleListing> result = await sender.Send(new ListVehiclesQuery(unitId, search, page ?? 1, pageSize ?? 50), cancellationToken);

        return ApiResults.Respond(result, response, listing => VehicleMapper.ToResponse(listing));
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<VehicleView> result = await sender.Send(new GetVehicleQuery(id), cancellationToken);

        return ApiResults.Respond(result, response, view => VehicleMapper.ToResponse(view));
    }

    private static async Task<IResult> RegisterAsync(VehicleRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<VehicleView> result = await sender.Send(VehicleMapper.ToRegisterCommand(request, userId), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.Created($"{VehicleRoutes.Prefix}/{result.Value.Id}", VehicleMapper.ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(Guid id, VehicleRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<VehicleView> result = await sender.Send(VehicleMapper.ToUpdateCommand(request, id, userId), cancellationToken);

        return ApiResults.Respond(result, response, view => VehicleMapper.ToResponse(view));
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result result = await sender.Send(new DeleteVehicleCommand(userId, id), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorHttp.ToProblem(result.Error);
        }

        response.PreventCaching();

        return Results.NoContent();
    }
}
