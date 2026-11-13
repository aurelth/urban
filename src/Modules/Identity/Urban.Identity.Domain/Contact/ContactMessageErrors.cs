using Urban.SharedKernel;

namespace Urban.Identity.Domain.Contact;

public static class ContactMessageErrors
{
    public static readonly Error SubjectInvalid =
        new("identity.contact.subject_invalid", $"O assunto deve ter de {ContactMessage.MinSubjectLength} a {ContactMessage.MaxSubjectLength} caracteres, em uma só linha");

    public static readonly Error BodyInvalid =
        new("identity.contact.body_invalid", $"A mensagem deve ter de {ContactMessage.MinBodyLength} a {ContactMessage.MaxBodyLength} caracteres");
}
