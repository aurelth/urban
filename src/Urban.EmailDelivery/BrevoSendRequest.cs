namespace Urban.EmailDelivery;

/// <summary>Corpo de <c>POST /v3/smtp/email</c> do Brevo, só com os campos que o Urban usa.</summary>
internal sealed record BrevoSendRequest(
    BrevoContact Sender,
    IReadOnlyList<BrevoContact> To,
    string Subject,
    string TextContent,
    BrevoContact? ReplyTo = null,
    IReadOnlyList<BrevoAttachment>? Attachment = null);

internal sealed record BrevoContact(string? Name, string Email);

/// <summary>O conteúdo do arquivo vai em base64: o <c>System.Text.Json</c> já grava um <c>byte[]</c> assim.</summary>
internal sealed record BrevoAttachment(string Name, byte[] Content);
