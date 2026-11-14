namespace Urban.Condominium.Application.Vehicles;

public sealed record VehicleListing(IReadOnlyList<VehicleView> Items, int Total, int Page, int PageSize);
