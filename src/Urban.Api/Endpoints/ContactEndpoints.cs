using System.Security.Claims;
using MediatR;
using Urban.Api.Http;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Contact;
using Urban.Identity.Application.Contact.ListContactMessages;
using Urban.Identity.Application.Contact.MarkContactMessageRead;
using Urban.Identity.Application.Contact.SendContactMessage;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class ContactEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";
    private const string UnreadFilter = "unread";

    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contact/messages", SendAsync)
            .RequireAuthorization()
            .WithTags("Contato")
            .WithName("SendContactMessage")
            .WithSummary("Envia uma mensagem ao dono da plataforma")
            .WithDescription(
                "Só o síndico com a conta ativa (403 para os demais). Assunto de 3 a 120 caracteres, em uma só linha, e mensagem de 10 a 2000 caracteres. "
                + "A mensagem fica registrada e o dono é avisado por e-mail; não há conversa dentro do sistema: ele responde por fora (201 com o comprovante). "
                + $"A resposta nunca é guardada em cache. {RateLimitText}")
            .RequireRateLimiting(RateLimitPolicies.Contact)
            .Produces<ContactMessageReceiptResponse>(StatusCodes.Status201Created)
            .WithErrors(StatusCodes.Status400BadRequest);

        RouteGroupBuilder inbox = app.MapGroup($"{AdminRoutes.Prefix}/contact-messages").WithTags("Administração");

        inbox.MapGet("/", ListAsync)
            .RequireAuthorization()
            .WithName("ListContactMessages")
            .WithSummary("Lista as mensagens de contato dos síndicos")
            .WithDescription(
                "Só o SuperAdmin ativo. Mostra as mensagens da mais nova para a mais antiga, com quem enviou e o nome real do condomínio. "
                + "Filtros: status (unread mostra só as não lidas), page (a partir de 1) e pageSize (de 1 a 100; o padrão é 20). "
                + $"A resposta nunca é guardada em cache. {RateLimitText}")
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces<ContactMessageListResponse>()
            .WithErrors(StatusCodes.Status400BadRequest);

        inbox.MapPost("/{id:guid}/read", MarkReadAsync)
            .RequireAuthorization()
            .WithName("MarkContactMessageRead")
            .WithSummary("Marca uma mensagem de contato como lida")
            .WithDescription(
                "Só o SuperAdmin ativo. Marcar de novo uma mensagem já lida não é erro e mantém o primeiro momento de leitura (204). "
                + $"Mensagem que não existe responde 404. {RateLimitText}")
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .Produces(StatusCodes.Status204NoContent)
            .WithErrors(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SendAsync(
        SendContactMessageRequest? request,
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        if (request is null)
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        Result<ContactMessageReceipt> result = await sender.Send(new SendContactMessageCommand(session.UserId, request.Subject, request.Body), cancellationToken);

        return ApiResults.Created(result, response, receipt => $"{AdminRoutes.Prefix}/contact-messages/{receipt.Id}", ContactMessageReceiptResponse.From);
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        string? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        if (!TryParseUnreadOnly(status, out bool unreadOnly))
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        Result<ContactMessageListing> result = await sender.Send(new ListContactMessagesQuery(session.UserId, unreadOnly, page ?? 1, pageSize ?? 20), cancellationToken);

        return ApiResults.Respond(result, response, ContactMessageListResponse.From);
    }

    private static async Task<IResult> MarkReadAsync(Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        Result result = await sender.Send(new MarkContactMessageReadCommand(session.UserId, id), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ErrorHttp.ToProblem(result.Error);
    }

    /// <summary>Texto vazio é "sem filtro"; "unread" pede só as não lidas; qualquer outro valor é erro de quem chamou.</summary>
    private static bool TryParseUnreadOnly(string? text, out bool unreadOnly)
    {
        unreadOnly = false;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        unreadOnly = string.Equals(text.Trim(), UnreadFilter, StringComparison.OrdinalIgnoreCase);

        return unreadOnly;
    }
}
