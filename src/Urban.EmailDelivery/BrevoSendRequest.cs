namespace Urban.EmailDelivery;

/// <summary>Corpo de <c>POST /v3/smtp/email</c> do Brevo, só com os campos que o Urban usa.</summary>
internal sealed record BrevoSendRequest(BrevoContact Sender, IReadOnlyList<BrevoContact> To, string Subject, string TextContent);

internal sealed record BrevoContact(string? Name, string Email);