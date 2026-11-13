using Urban.Identity.Application.Administration;

namespace Urban.Api.Endpoints;

public sealed record PlatformSummaryResponse(
    int ActiveManagers,
    int SuspendedManagers,
    int AwaitingConfirmationManagers,
    int PendingRequests,
    int Condominiums)
{
    public static PlatformSummaryResponse From(PlatformSummary summary) => new(
        summary.ActiveManagers,
        summary.SuspendedManagers,
        summary.AwaitingConfirmationManagers,
        summary.PendingRequests,
        summary.Condominiums);
}

public sealed record ManagerResponse(
    Guid Id,
    string Email,
    string? Name,
    Guid? CondominiumId,
    string? CondominiumName,
    string Status,
    string? StatusReason,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset CreatedAt)
{
    public static ManagerResponse From(ManagerSummary manager) => new(
        manager.Id,
        manager.Email,
        manager.Name,
        manager.CondominiumId,
        manager.CondominiumName,
        manager.Status.ToString(),
        manager.StatusReason,
        manager.StatusChangedAt,
        manager.CreatedAt);
}

public sealed record ManagerListResponse(ManagerResponse[] Items, int Total, int Page, int PageSize)
{
    public static ManagerListResponse From(ManagerListing listing) =>
        new([.. listing.Items.Select(ManagerResponse.From)], listing.Total, listing.Page, listing.PageSize);
}

public sealed record SuspendManagerRequest(string? Reason);
