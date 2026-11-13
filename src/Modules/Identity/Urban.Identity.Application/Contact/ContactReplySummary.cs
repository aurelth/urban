using Urban.Identity.Domain.Contact;

namespace Urban.Identity.Application.Contact;

public sealed record ContactReplySummary(Guid Id, DateTimeOffset SentAt, string Body, IReadOnlyList<ContactReplyAttachment> Attachments)
{
    public static ContactReplySummary From(ContactReply reply) => new(reply.Id, reply.SentAt, reply.Body, reply.Attachments);
}
