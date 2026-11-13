using Urban.Identity.Domain.Contact;

namespace Urban.Identity.Application.Contact;

public interface IContactReplies
{
    Task AddAsync(ContactReply reply, CancellationToken cancellationToken);
}
