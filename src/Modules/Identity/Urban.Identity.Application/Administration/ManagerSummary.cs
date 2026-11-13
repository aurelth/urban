using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Administration;

/// <param name="Name">Como o síndico prefere ser chamado; nulo em contas antigas, que ainda não o escolheram.</param>
/// <param name="CondominiumName">O nome real do condomínio; antes do cadastro dele, o nome que a pessoa digitou no pedido.</param>
public sealed record ManagerSummary(
    Guid Id,
    string Email,
    string? Name,
    Guid? CondominiumId,
    string? CondominiumName,
    AccountStatus Status,
    string? StatusReason,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset CreatedAt)
{
    public static ManagerSummary From(User user, string? condominiumName) => new(
        user.Id,
        user.Email.Value,
        user.DisplayName?.Value,
        user.CondominiumId,
        condominiumName ?? user.RequestedCondominiumName?.Value,
        user.Status,
        user.StatusReason,
        user.StatusChangedAt,
        user.CreatedAt);
}
