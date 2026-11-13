namespace Urban.Identity.Application.Administration;

public sealed record ManagerListing(IReadOnlyList<ManagerSummary> Items, int Total, int Page, int PageSize);
