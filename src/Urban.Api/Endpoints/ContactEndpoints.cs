using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http.Features;
using Urban.Api.Http;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Contact;
using Urban.Identity.Application.Contact.ListContactMessages;
using Urban.Identity.Application.Contact.MarkContactMessageRead;
using Urban.Identity.Application.Contact.SendContactMessage;
using Urban.Identity.Application.Contact.SendContactReply;
using Urban.Identity.Domain.Contact;
using Urban.SharedKernel;

namespace Urban.Api.Endpoints;

public static class ContactEndpoints
{
    private const string RateLimitText = "O mesmo IP tem um limite de requisições por minuto; ao exceder, recebe 429 com Retry-After.";
    private const string UnreadFilter = "unread";
    private const string BodyField = "body";
    private const string AttachmentsField = "attachments";

    /// <summary>Três arquivos de 5 MB mais o texto, com folga para o formato multipart; acima disso o Kestrel recusa antes de ler.</summary>
    private const long MaxReplyRequestBytes = 16 * 1024 * 1024;

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

        // O acesso é por token no cabeçalho (não por cookie), então o formulário não está sujeito a falsificação de requisição entre sites.
        inbox.MapPost("/{id:guid}/replies", ReplyAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithName("SendContactReply")
            .WithSummary("Responde a uma mensagem de contato por e-mail")
            .WithDescription(
                "Só o SuperAdmin ativo. Corpo multipart/form-data: o campo body (texto de 5 a 5000 caracteres) e, em attachments, até 3 arquivos "
                + "(PDF, PNG, JPG, DOCX, XLSX, CSV ou TXT, até 5 MB cada e 10 MB no total; o conteúdo é conferido, não só a extensão). "
                + "O e-mail sai na hora para o síndico, com o administrador em Reply-To, e só então a resposta é gravada e a mensagem vira lida; "
                + "o Urban guarda o texto e nome, tipo e tamanho dos arquivos, nunca o conteúdo. Se o provedor recusar o envio, 502 e nada é gravado. "
                + $"A resposta nunca é guardada em cache. {RateLimitText}")
            .RequireRateLimiting(RateLimitPolicies.ContactReply)
            .Produces<ContactReplyReceiptResponse>(StatusCodes.Status201Created)
            .WithErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status502BadGateway);

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

    private static async Task<IResult> ReplyAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal user,
        HttpResponse response,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            return ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
        }

        if (!request.HasFormContentType)
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        IHttpMaxRequestBodySizeFeature? limit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = MaxReplyRequestBytes;
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return ErrorHttp.ToProblem(ContactReplyErrors.AttachmentsTooLarge);
        }
        catch (Exception ex) when (ex is BadHttpRequestException or InvalidDataException)
        {
            return ErrorHttp.ToProblem(RequestErrors.Invalid);
        }

        ContactReplyFile[] files = await ReadFilesAsync(form.Files.GetFiles(AttachmentsField), cancellationToken);
        Result<ContactReplyReceipt> result = await sender.Send(
            new SendContactReplyCommand(session.UserId, id, form[BodyField].FirstOrDefault(), files),
            cancellationToken);

        return ApiResults.Created(result, response, receipt => $"{AdminRoutes.Prefix}/contact-messages/{id}/replies/{receipt.Id}", ContactReplyReceiptResponse.From);
    }

    /// <summary>O pedido inteiro já está limitado a <see cref="MaxReplyRequestBytes"/>, então ler os arquivos para a memória tem teto.</summary>
    private static async Task<ContactReplyFile[]> ReadFilesAsync(IReadOnlyList<IFormFile> uploads, CancellationToken cancellationToken)
    {
        List<ContactReplyFile> files = [];
        foreach (IFormFile upload in uploads)
        {
            using MemoryStream content = new((int)upload.Length);
            await upload.CopyToAsync(content, cancellationToken);
            files.Add(new ContactReplyFile(upload.FileName, content.ToArray()));
        }

        return [.. files];
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
