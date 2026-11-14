namespace Urban.Condominium.Domain.Vehicles;

/// <summary>Os dados de um veículo já validados e normalizados, prontos para gravar ou para comparar com o que está gravado.</summary>
public sealed record VehicleData(string Plate, string Model, string Color);
