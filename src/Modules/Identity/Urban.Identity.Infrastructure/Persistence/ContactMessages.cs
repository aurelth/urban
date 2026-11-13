using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Contact;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>Caixa de entrada do dono. A marcação como lida é um único UPDATE condicionado a ainda não ter leitura, que o banco aplica de forma atômica.</summary>
public sealed class ContactMessages(IdentityDbContext db) : IContactMessages
{
    public async Task AddAsync(ContactMessage message, CancellationToken cancellationToken)
    {
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ContactMessage?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.ContactMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<ContactMessagePage> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<ContactMessage> filtered = db.ContactMessages.AsNoTracking();
        if (unreadOnly)
        {
            filtered = filtered.Where(m => m.ReadAt == null);
        }

        int total = await filtered.CountAsync(cancellationToken);

        // O e-mail e os nomes do usuário têm conversores de valor, por isso a entidade inteira é lida e os resumos são montados depois.
        List<ContactMessageEntry> items = await (
            from message in filtered
            join sender in db.Users.AsNoTracking() on message.SenderId equals sender.Id
            orderby message.CreatedAt descending, message.Id descending
            select new ContactMessageEntry(message, sender))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ContactMessagePage(await WithRepliesAsync(items, cancellationToken), total);
    }

    /// <summary>Uma única consulta traz as respostas da página inteira, da mais antiga para a mais nova.</summary>
    private async Task<List<ContactMessageEntry>> WithRepliesAsync(List<ContactMessageEntry> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        Guid[] ids = [.. items.Select(e => e.Message.Id)];
        List<ContactReply> replies = await db.ContactReplies
            .AsNoTracking()
            .Where(r => ids.Contains(r.MessageId))
            .OrderBy(r => r.SentAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);
        ILookup<Guid, ContactReply> byMessage = replies.ToLookup(r => r.MessageId);

        return [.. items.Select(e => byMessage.Contains(e.Message.Id) ? e with { Replies = [.. byMessage[e.Message.Id]] } : e)];
    }

    public async Task MarkReadAsync(ContactMessage message, CancellationToken cancellationToken)
    {
        await db.ContactMessages
            .Where(m => m.Id == message.Id && m.ReadAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.ReadAt, message.ReadAt), cancellationToken);
    }
}
