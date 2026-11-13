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
}
