using Urban.Identity.Application.Contact;
using Urban.Identity.Domain.Contact;

namespace Urban.Identity.Infrastructure.Persistence;

public sealed class ContactReplies(IdentityDbContext db) : IContactReplies
{
    public async Task AddAsync(ContactReply reply, CancellationToken cancellationToken)
    {
        db.ContactReplies.Add(reply);
        await db.SaveChangesAsync(cancellationToken);
    }
}
