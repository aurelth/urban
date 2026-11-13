using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Contact;

/// <param name="SenderName">Como o síndico prefere ser chamado; nulo se ainda não escolheu.</param>
/// <param name="CondominiumName">O nome real do condomínio; antes do cadastro dele, o nome digitado no pedido; nulo se não há nenhum.</param>
public sealed record ContactMessageSummary(
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
    public static ContactMessageSummary From(ContactMessage message, User sender, string? condominiumName) => new(
        message.Id,
        sender.Id,
        sender.Email.Value,
        sender.DisplayName?.Value,
        condominiumName ?? sender.RequestedCondominiumName?.Value,
        message.Subject,
        message.Body,
        message.CreatedAt,
        message.ReadAt);
}
