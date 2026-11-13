using Urban.SharedKernel;

namespace Urban.Identity.Domain.Contact;

/// <summary>
/// A resposta do dono a uma mensagem de contato, enviada por e-mail ao síndico. O Urban guarda o texto e os metadados dos arquivos
/// (nunca o conteúdo), para a caixa de entrada mostrar o que já foi respondido.
/// </summary>
public sealed class ContactReply
{
    public const int MinBodyLength = 5;
    public const int MaxBodyLength = 5000;
    public const int MaxAttachments = 3;
    public const int MaxTotalBytes = 10 * 1024 * 1024;

    private ContactReply(Guid id, Guid messageId, Guid authorId, string body, IReadOnlyList<ContactReplyAttachment> attachments, DateTimeOffset sentAt)
    {
        Id = id;
        MessageId = messageId;
        AuthorId = authorId;
        Body = body;
        Attachments = attachments;
        SentAt = sentAt;
    }

    public Guid Id { get; }

    public Guid MessageId { get; }

    public Guid AuthorId { get; }

    public string Body { get; }

    public IReadOnlyList<ContactReplyAttachment> Attachments { get; }

    public DateTimeOffset SentAt { get; }

    /// <summary>Confere o texto primeiro, depois a quantidade e cada arquivo na ordem recebida, e por fim o tamanho somado.</summary>
    public static Result<ContactReply> Create(Guid messageId, Guid authorId, string? body, IReadOnlyList<ContactReplyFile> files, DateTimeOffset now)
    {
        string cleanBody = body?.Trim() ?? string.Empty;
        if (cleanBody.Length is < MinBodyLength or > MaxBodyLength || cleanBody.Any(ContactMessage.IsForbiddenInBody))
        {
            return Result.Failure<ContactReply>(ContactReplyErrors.BodyInvalid);
        }

        if (files.Count > MaxAttachments)
        {
            return Result.Failure<ContactReply>(ContactReplyErrors.TooManyAttachments);
        }

        List<ContactReplyAttachment> attachments = [];
        foreach (ContactReplyFile file in files)
        {
            Result<ContactReplyAttachment> attachment = ContactReplyAttachment.From(file.FileName, file.Content);
            if (!attachment.IsSuccess)
            {
                return Result.Failure<ContactReply>(attachment.Error);
            }

            attachments.Add(attachment.Value);
        }

        if (attachments.Sum(a => a.Size) > MaxTotalBytes)
        {
            return Result.Failure<ContactReply>(ContactReplyErrors.AttachmentsTooLarge);
        }

        return Result.Success(new ContactReply(Guid.CreateVersion7(), messageId, authorId, cleanBody, attachments, now));
    }
}
