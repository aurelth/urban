using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Contact;

/// <param name="Replies">As respostas da mensagem, da mais antiga para a mais nova; nulo ou vazio se ninguém respondeu.</param>
public sealed record ContactMessageEntry(ContactMessage Message, User Sender, IReadOnlyList<ContactReply>? Replies = null);
