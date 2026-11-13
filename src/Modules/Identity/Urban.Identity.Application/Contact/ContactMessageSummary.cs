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
    /// <summary>Da mais antiga para a mais nova. Fica fora do construtor para quem só quer a mensagem não precisar informá-la.</summary>
    public IReadOnlyList<ContactReplySummary> Replies { get; init; } = [];

    public static ContactMessageSummary From(ContactMessage message, User sender, string? condominiumName, IReadOnlyList<ContactReply>? replies = null) => new(
        message.Id,
        sender.Id,
        sender.Email.Value,
        sender.DisplayName?.Value,
        condominiumName ?? sender.RequestedCondominiumName?.Value,
        message.Subject,
        message.Body,
        message.CreatedAt,
        message.ReadAt)
    {
        Replies = replies is { Count: > 0 } ? [.. replies.Select(ContactReplySummary.From)] : [],
    };
}
