namespace Urban.Condominium.Application.Abstractions;

/// <summary>Quais moradores a lista traz. Nulo é "sem esse filtro".</summary>
/// <param name="Search">Trecho do nome, sem diferenciar maiúsculas de minúsculas.</param>
/// <param name="UnitId">Só quem tem vínculo com esta unidade.</param>
public sealed record ResidentFilter(string? Search, Guid? UnitId);
