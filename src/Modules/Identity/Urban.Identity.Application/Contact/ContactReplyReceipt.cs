using Urban.Identity.Domain.Contact;

namespace Urban.Identity.Application.Contact;

public sealed record ContactReplyReceipt(Guid Id, DateTimeOffset SentAt, IReadOnlyList<ContactReplyAttachment> Attachments);
