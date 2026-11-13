using Urban.Identity.Domain.Contact;

namespace Urban.TestSupport.Identity;

public static class ContactMessageBuilder
{
    public static ContactMessage Unread(
        Guid senderId,
        Guid? condominiumId = null,
        string subject = "Corrigir o CNPJ",
        string body = "O CNPJ do meu condomínio está errado.",
        DateTimeOffset? sentAt = null) =>
        ContactMessage.Send(senderId, condominiumId, subject, body, sentAt ?? UserBuilder.Now).Value;

    public static ContactMessage Read(
        Guid senderId,
        DateTimeOffset readAt,
        Guid? condominiumId = null,
        string subject = "Corrigir o CNPJ",
        string body = "O CNPJ do meu condomínio está errado.",
        DateTimeOffset? sentAt = null)
    {
        ContactMessage message = Unread(senderId, condominiumId, subject, body, sentAt);
        message.MarkRead(readAt);

        return message;
    }
}
