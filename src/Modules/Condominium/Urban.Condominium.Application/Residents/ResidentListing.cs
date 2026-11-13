namespace Urban.Condominium.Application.Residents;

public sealed record ResidentListing(IReadOnlyList<ResidentView> Items, int Total, int Page, int PageSize);
