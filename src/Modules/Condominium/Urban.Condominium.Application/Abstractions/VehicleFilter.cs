namespace Urban.Condominium.Application.Abstractions;

/// <summary>Quais veículos a lista traz. Nulo é "sem esse filtro".</summary>
/// <param name="Search">Trecho da placa (sem diferenciar maiúsculas nem hífen), do modelo ou da cor.</param>
public sealed record VehicleFilter(Guid? UnitId, string? Search);
