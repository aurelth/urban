using Urban.Identity.Domain.Contact;

namespace Urban.TestSupport.Identity;

public static class ContactReplyBuilder
{
    public static ContactReply Text(
        Guid messageId,
        Guid authorId,
        string body = "Corrigimos o CNPJ do condomínio.",
        DateTimeOffset? sentAt = null) =>
        ContactReply.Create(messageId, authorId, body, [], sentAt ?? UserBuilder.Now).Value;

    public static ContactReply WithFiles(
        Guid messageId,
        Guid authorId,
        IReadOnlyList<ContactReplyFile> files,
        string body = "Segue o documento.",
        DateTimeOffset? sentAt = null) =>
        ContactReply.Create(messageId, authorId, body, files, sentAt ?? UserBuilder.Now).Value;
}
