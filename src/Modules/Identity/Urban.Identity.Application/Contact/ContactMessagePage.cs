namespace Urban.Identity.Application.Contact;

public sealed record ContactMessagePage(IReadOnlyList<ContactMessageEntry> Items, int Total);
