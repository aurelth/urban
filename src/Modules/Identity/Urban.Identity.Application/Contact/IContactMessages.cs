using Urban.Identity.Domain.Contact;

namespace Urban.Identity.Application.Contact;

public interface IContactMessages
{
    Task AddAsync(ContactMessage message, CancellationToken cancellationToken);

    Task<ContactMessage?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Das mais novas para as mais antigas, cada uma com a conta de quem enviou; <paramref name="page"/> começa em 1.</summary>
    Task<ContactMessagePage> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Grava só o momento da leitura, e só se a mensagem ainda não tiver um: duas leituras ao mesmo tempo mantêm a primeira.</summary>
    Task MarkReadAsync(ContactMessage message, CancellationToken cancellationToken);
}
