using Urban.Identity.Application.Contact;

namespace Urban.Api.Endpoints;

public sealed record SendContactMessageRequest(string? Subject, string? Body);

public sealed record ContactMessageReceiptResponse(Guid Id, DateTimeOffset SentAt)
{
    public static ContactMessageReceiptResponse From(ContactMessageReceipt receipt) => new(receipt.Id, receipt.SentAt);
}

public sealed record ContactMessageResponse(
    Guid Id,
    Guid SenderId,
    string SenderEmail,
    string? SenderName,
    string? CondominiumName,
    string Subject,
    string Body,
    DateTimeOffset SentAt,
    DateTimeOffset? ReadAt)
{
    public static ContactMessageResponse From(ContactMessageSummary message) => new(
        message.Id,
        message.SenderId,
        message.SenderEmail,
        message.SenderName,
        message.CondominiumName,
        message.Subject,
        message.Body,
        message.SentAt,
        message.ReadAt);
}

public sealed record ContactMessageListResponse(ContactMessageResponse[] Items, int Total, int Page, int PageSize)
{
    public static ContactMessageListResponse From(ContactMessageListing listing) =>
        new([.. listing.Items.Select(ContactMessageResponse.From)], listing.Total, listing.Page, listing.PageSize);
}
