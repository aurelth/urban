using Urban.SharedKernel;

namespace Urban.Identity.Domain.Contact;

public static class ContactReplyErrors
{
    public static readonly Error BodyInvalid =
        new("identity.contact.reply_body_invalid", $"A resposta deve ter de {ContactReply.MinBodyLength} a {ContactReply.MaxBodyLength} caracteres");

    public static readonly Error AttachmentNameInvalid =
        new("identity.contact.attachment_name_invalid", $"O nome do arquivo deve ter até {ContactReplyAttachment.MaxFileNameLength} caracteres e não pode ter símbolos como < > : \" | ? *");

    public static readonly Error AttachmentTypeNotAllowed =
        new("identity.contact.attachment_type_not_allowed", "Só são aceitos arquivos PDF, PNG, JPG, DOCX, XLSX, CSV e TXT");

    public static readonly Error AttachmentContentInvalid =
        new("identity.contact.attachment_content_invalid", "O arquivo está vazio ou o conteúdo não é do tipo que o nome indica");

    public static readonly Error AttachmentTooLarge =
        new("identity.contact.attachment_too_large", $"Cada arquivo pode ter até {ContactReplyAttachment.MaxFileBytes / (1024 * 1024)} MB");

    public static readonly Error TooManyAttachments =
        new("identity.contact.attachments_too_many", $"Uma resposta pode ter até {ContactReply.MaxAttachments} arquivos");

    public static readonly Error AttachmentsTooLarge =
        new("identity.contact.attachments_total_too_large", $"Os arquivos de uma resposta podem somar até {ContactReply.MaxTotalBytes / (1024 * 1024)} MB");
}
