using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Api.Mapping;
using Urban.Operations.Application.Announcements;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

/// <summary>Os comunicados do síndico aos moradores: o síndico escreve e acompanha quem leu; quem mora lê o mural e marca como lido.</summary>
public static class AnnouncementEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";

    private const string VersionText = "A versão lida na listagem vai no corpo (400 sem ela, 409 se outra pessoa mudou antes).";

    private const string AudienceText =
        "O alcance (scope) é condominium (todos), block (só o bloco informado em block, que precisa ter unidades) ou unit (só a unidade informada em unitId, que precisa existir no condomínio): bloco ou unidade que não existem respondem 422. "
        + "A categoria é general, maintenance ou important. O título tem até 120 caracteres em uma linha e o texto, até 5000, com quebras de linha (400 se passar ou vier vazio).";

    public static IEndpointRouteBuilder MapAnnouncementEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder announcements = app.MapGroup(AnnouncementRoutes.Prefix).WithTags("Comunicados").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Announcements);

        announcements.MapGet("/", ListAsync)
            .WithName("ListAnnouncements")
            .WithSummary("Lista os comunicados do condomínio, com quantos já leram")
            .WithDescription(
                "Só o síndico. Do mais novo ao mais velho, uma página por vez (page a partir de 1 e pageSize de 1 a 100, padrão 20; 400 fora disso). Cada comunicado traz quantas contas moram hoje no alcance dele (recipients, sem o síndico) e quantas já marcaram como lido (readers), "
                + "e a versão que a edição e a exclusão devolvem. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<AnnouncementPageResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        announcements.MapPost("/", CreateAsync)
            .WithName("CreateAnnouncement")
            .WithSummary("Cria um comunicado")
            .WithDescription("Só o síndico. " + AudienceText + " Com `notifyByEmail: true`, quem mora no alcance também recebe o comunicado no e-mail, uma vez, ao publicar (sem o campo, não há e-mail; editar nunca reenvia). Devolve 201 com o comunicado. " + RateLimitText)
            .Produces<AnnouncementResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);

        announcements.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateAnnouncement")
            .WithSummary("Edita um comunicado")
            .WithDescription("Só o síndico. Troca tudo de uma vez; quem já leu continua tendo lido. " + VersionText + " " + AudienceText + " " + RateLimitText)
            .Produces<AnnouncementResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        announcements.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteAnnouncement")
            .WithSummary("Apaga um comunicado, com as leituras dele")
            .WithDescription("Só o síndico. Responde 204. A versão vai na query (`?version=`): sem ela, 400; versão velha, 409. " + RateLimitText)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        announcements.MapGet("/mine", ListMineAsync)
            .WithName("ListMyAnnouncements")
            .WithSummary("O mural de quem mora no condomínio")
            .WithDescription(
                "Para o morador, o inquilino, o dependente e o síndico que mora numa unidade. Devolve os comunicados do condomínio inteiro, do bloco e da unidade em que a conta mora hoje (vínculo em andamento, de qualquer papel), do mais novo ao mais velho, "
                + "cada um com read e readAt e, conforme o alcance, o bloco (block) ou a unidade (unit, com id, bloco e número) a que se destina. "
                + "unread conta os que a conta ainda não leu no mural inteiro e unreadByCategory os separa em general, maintenance e important; nenhum filtro nem a página mudam essas contagens. "
                + "Com unreadOnly=true vêm só os não lidos e com category=general, maintenance ou important vêm só os dessa categoria (em branco, todas; outro nome é 400); os dois filtros valem juntos e o total os acompanha. "
                + "page a partir de 1 e pageSize de 1 a 100, padrão 20 (400 fora disso). Quem não mora em nenhuma unidade recebe o mural vazio. Os comunicados que a própria conta escreveu não aparecem. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<MyAnnouncementPageResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        announcements.MapPost("/read-all", MarkAllReadAsync)
            .WithName("MarkAllAnnouncementsRead")
            .WithSummary("Marca todos os comunicados do mural como lidos")
            .WithDescription(
                "Para quem recebe comunicados. Marca de uma vez os que a conta ainda não leu no mural (só os da categoria, se category=general, maintenance ou important vier; em branco, todas; outro nome é 400) e responde 200 com marked, "
                + "quantos foram marcados agora. Os que a conta já tinha lido não mudam de data e não entram na conta; chamar de novo responde marked 0. Quem não mora em nenhuma unidade recebe marked 0. A resposta nunca é guardada em cache. " + RateLimitText)
            .Produces<MarkedReadResponse>()
            .WithErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);

        announcements.MapPost("/{id:guid}/read", MarkReadAsync)
            .WithName("MarkAnnouncementRead")
            .WithSummary("Marca um comunicado como lido")
            .WithDescription(
                "Para quem recebe o comunicado. Responde 204, também quando a conta já tinha marcado (a data da primeira leitura não muda). Comunicado que não existe, de outro bloco ou de outra unidade, ou escrito pela própria conta, responde 404. " + RateLimitText)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListAsync(int? page, int? pageSize, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        return ApiResults.Respond(await sender.Send(new ListAnnouncementsQuery(page ?? 1, pageSize ?? 20), cancellationToken), response, AnnouncementMapper.ToResponse);
    }

    private static async Task<IResult> CreateAsync(AnnouncementRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<AnnouncementView> result = await sender.Send(
            new CreateAnnouncementCommand(userId, request.Title, request.Body, request.Category, request.Scope, request.Block, request.UnitId, request.NotifyByEmail == true), cancellationToken);

        return ApiResults.Created(result, response, view => $"{AnnouncementRoutes.Prefix}/{view.Id}", AnnouncementMapper.ToResponse);
    }

    private static async Task<IResult> UpdateAsync(Guid id, AnnouncementRequest request, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result<AnnouncementView> result = await sender.Send(
            new UpdateAnnouncementCommand(id, request.Version, request.Title, request.Body, request.Category, request.Scope, request.Block, request.UnitId), cancellationToken);

        return ApiResults.Respond(result, response, AnnouncementMapper.ToResponse);
    }

    private static async Task<IResult> DeleteAsync(Guid id, string? version, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!ManagerAccess.TryRead(user, out _, out IResult? refusal))
        {
            return refusal;
        }

        Result result = await sender.Send(new DeleteAnnouncementCommand(id, version), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }

    private static async Task<IResult> ListMineAsync(
        bool? unreadOnly, string? category, int? page, int? pageSize, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!MemberAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result<MyAnnouncementPageView> result = await sender.Send(new ListMyAnnouncementsQuery(userId, unreadOnly ?? false, page ?? 1, pageSize ?? 20, category), cancellationToken);

        return ApiResults.Respond(result, response, AnnouncementMapper.ToResponse);
    }

    private static async Task<IResult> MarkAllReadAsync(string? category, ClaimsPrincipal user, HttpResponse response, ISender sender, CancellationToken cancellationToken)
    {
        if (!MemberAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        return ApiResults.Respond(await sender.Send(new MarkAllAnnouncementsReadCommand(userId, category), cancellationToken), response, AnnouncementMapper.ToMarkedResponse);
    }

    private static async Task<IResult> MarkReadAsync(Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!MemberAccess.TryRead(user, out Guid userId, out IResult? refusal))
        {
            return refusal;
        }

        Result result = await sender.Send(new MarkAnnouncementReadCommand(userId, id), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }
}
