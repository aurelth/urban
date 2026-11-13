using Urban.SharedKernel;

namespace Urban.Identity.Domain.Contact;

/// <summary>
/// Mensagem que um síndico envia ao dono da plataforma. É um registro simples, sem conversa dentro do sistema: o dono lê,
/// marca como lida e responde por fora. O primeiro momento de leitura nunca muda depois de gravado.
/// </summary>
public sealed class ContactMessage
{
    public const int MinSubjectLength = 3;
    public const int MaxSubjectLength = 120;
    public const int MinBodyLength = 10;
    public const int MaxBodyLength = 2000;

    private ContactMessage(Guid id, Guid senderId, Guid? condominiumId, string subject, string body, DateTimeOffset createdAt, DateTimeOffset? readAt)
    {
        Id = id;
        SenderId = senderId;
        CondominiumId = condominiumId;
        Subject = subject;
        Body = body;
        CreatedAt = createdAt;
        ReadAt = readAt;
    }

    public Guid Id { get; }

    public Guid SenderId { get; }

    /// <summary>O condomínio do síndico no momento do envio; nulo se ele ainda não o cadastrou.</summary>
    public Guid? CondominiumId { get; }

    public string Subject { get; }

    public string Body { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Result<ContactMessage> Send(Guid senderId, Guid? condominiumId, string? subject, string? body, DateTimeOffset now)
    {
        string cleanSubject = subject?.Trim() ?? string.Empty;
        if (cleanSubject.Length is < MinSubjectLength or > MaxSubjectLength || cleanSubject.Any(char.IsControl))
        {
            return Result.Failure<ContactMessage>(ContactMessageErrors.SubjectInvalid);
        }

        string cleanBody = body?.Trim() ?? string.Empty;
        if (cleanBody.Length is < MinBodyLength or > MaxBodyLength || cleanBody.Any(IsForbiddenInBody))
        {
            return Result.Failure<ContactMessage>(ContactMessageErrors.BodyInvalid);
        }

        return Result.Success(new ContactMessage(Guid.CreateVersion7(), senderId, condominiumId, cleanSubject, cleanBody, now, null));
    }

    /// <returns><c>true</c> se esta chamada registrou a leitura; <c>false</c> se a mensagem já estava lida (o primeiro momento é mantido).</returns>
    public bool MarkRead(DateTimeOffset now)
    {
        if (IsRead)
        {
            return false;
        }

        ReadAt = now;
        return true;
    }

    private static bool IsForbiddenInBody(char c) => char.IsControl(c) && c is not ('\n' or '\r' or '\t');
}
