using Urban.SharedKernel;

namespace Urban.Identity.Domain.Contact;

/// <summary>
/// O que o Urban guarda de um arquivo enviado na resposta: nome, tipo e tamanho. O conteúdo vai direto ao provedor de e-mail e
/// é descartado. <see cref="From"/> é a única porta de entrada: confere o nome, a extensão (lista fechada), o tamanho e o
/// conteúdo real, porque a extensão sozinha é só o que quem enviou declarou.
/// </summary>
public sealed record ContactReplyAttachment(string FileName, string ContentType, long Size)
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxFileNameLength = 100;

    private static readonly char[] ForbiddenInName = ['<', '>', ':', '"', '|', '?', '*'];

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".csv"] = "text/csv",
        [".txt"] = "text/plain",
    };

    public static Result<ContactReplyAttachment> From(string? fileName, byte[]? content)
    {
        string? name = CleanName(fileName);
        if (name is null)
        {
            return Result.Failure<ContactReplyAttachment>(ContactReplyErrors.AttachmentNameInvalid);
        }

        string extension = Path.GetExtension(name);
        if (!ContentTypes.TryGetValue(extension, out string? contentType))
        {
            return Result.Failure<ContactReplyAttachment>(ContactReplyErrors.AttachmentTypeNotAllowed);
        }

        if (content is not { Length: > 0 })
        {
            return Result.Failure<ContactReplyAttachment>(ContactReplyErrors.AttachmentContentInvalid);
        }

        if (content.Length > MaxFileBytes)
        {
            return Result.Failure<ContactReplyAttachment>(ContactReplyErrors.AttachmentTooLarge);
        }

        return ContactReplyAttachmentContent.Matches(extension, content)
            ? Result.Success(new ContactReplyAttachment(name, contentType, content.Length))
            : Result.Failure<ContactReplyAttachment>(ContactReplyErrors.AttachmentContentInvalid);
    }

    /// <summary>Fica só o nome do arquivo (sem pasta, que o navegador ou o sistema podem mandar) e sem caracteres que enganam ou quebram.</summary>
    private static string? CleanName(string? raw)
    {
        string name = raw?.Trim() ?? string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..].Trim();

        bool invalid = name.Length is 0 or > MaxFileNameLength
            || name.Any(char.IsControl)
            || name.IndexOfAny(ForbiddenInName) >= 0
            || name.Length == Path.GetExtension(name).Length;

        return invalid ? null : name;
    }
}
