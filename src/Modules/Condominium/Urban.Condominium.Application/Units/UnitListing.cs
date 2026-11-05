namespace Urban.Condominium.Application.Units;

public sealed record UnitListing(IReadOnlyList<UnitView> Items, int Total, int Page, int PageSize);
