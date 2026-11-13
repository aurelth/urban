using Urban.Identity.Domain.Contact;

namespace Urban.Identity.Application.Contact;

/// <param name="To">O síndico que escreveu a mensagem.</param>
/// <param name="RecipientName">Como o síndico prefere ser chamado; nulo se ainda não escolheu.</param>
/// <param name="ReplyTo">O administrador que responde: a resposta do síndico a este e-mail vai para ele.</param>
/// <param name="Files">Já conferidos, com o nome limpo.</param>
public sealed record ContactReplyEmail(
    string To,
    string? RecipientName,
    string ReplyTo,
    string OriginalSubject,
    DateTimeOffset OriginalSentAt,
    string Body,
    IReadOnlyList<ContactReplyFile> Files)
{
    public override string ToString() => nameof(ContactReplyEmail);
}
