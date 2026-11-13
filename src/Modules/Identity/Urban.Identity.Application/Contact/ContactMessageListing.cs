namespace Urban.Identity.Application.Contact;

public sealed record ContactMessageListing(IReadOnlyList<ContactMessageSummary> Items, int Total, int Page, int PageSize);
