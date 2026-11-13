using Urban.SharedKernel;

namespace Urban.Identity.Application.Contact;

public static class ContactErrors
{
    public static readonly Error SendForbidden =
        new("identity.contact.send_forbidden", "Só o síndico com a conta ativa pode enviar mensagem ao Urban");

    public static readonly Error ReadForbidden =
        new("identity.contact.read_forbidden", "Só o administrador pode ver as mensagens de contato");

    public static readonly Error PageInvalid =
        new("identity.contact.page_invalid", "A página deve ser 1 ou maior e o tamanho, de 1 a 100");

    public static readonly Error MessageNotFound =
        new("identity.contact.message_not_found", "Mensagem não encontrada");

    public static readonly Error ReplyForbidden =
        new("identity.contact.reply_forbidden", "Só o administrador pode responder às mensagens de contato");

    public static readonly Error ReplyRecipientUnavailable =
        new("identity.contact.reply_recipient_unavailable", "Quem escreveu a mensagem não tem mais uma conta, então não há para quem responder");

    public static readonly Error ReplyNotDelivered =
        new("identity.contact.reply_not_delivered", "O e-mail não foi entregue ao provedor de envio. Tente de novo em instantes");
}
