using Urban.Identity.Application.Contact;
using Urban.Identity.Domain.Contact;

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
    DateTimeOffset? ReadAt,
    ContactReplyResponse[] Replies)
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
        message.ReadAt,
        [.. message.Replies.Select(ContactReplyResponse.From)]);
}

/// <summary>Só nome, tipo e tamanho: o Urban não guarda o conteúdo dos arquivos.</summary>
public sealed record ContactReplyAttachmentResponse(string FileName, string ContentType, long Size)
{
    public static ContactReplyAttachmentResponse From(ContactReplyAttachment attachment) => new(attachment.FileName, attachment.ContentType, attachment.Size);
}

public sealed record ContactReplyResponse(Guid Id, DateTimeOffset SentAt, string Body, ContactReplyAttachmentResponse[] Attachments)
{
    public static ContactReplyResponse From(ContactReplySummary reply) =>
        new(reply.Id, reply.SentAt, reply.Body, [.. reply.Attachments.Select(ContactReplyAttachmentResponse.From)]);
}

public sealed record ContactReplyReceiptResponse(Guid Id, DateTimeOffset SentAt, ContactReplyAttachmentResponse[] Attachments)
{
    public static ContactReplyReceiptResponse From(ContactReplyReceipt receipt) =>
        new(receipt.Id, receipt.SentAt, [.. receipt.Attachments.Select(ContactReplyAttachmentResponse.From)]);
}

public sealed record ContactMessageListResponse(ContactMessageResponse[] Items, int Total, int Page, int PageSize)
{
    public static ContactMessageListResponse From(ContactMessageListing listing) =>
        new([.. listing.Items.Select(ContactMessageResponse.From)], listing.Total, listing.Page, listing.PageSize);
}
