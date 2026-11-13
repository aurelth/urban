using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Contact;

public sealed record ContactMessageEntry(ContactMessage Message, User Sender);
