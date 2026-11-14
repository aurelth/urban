namespace Urban.Condominium.Application.Abstractions;

/// <summary>Quais moradores a lista traz. Nulo é "sem esse filtro".</summary>
/// <param name="Search">Trecho do nome, sem diferenciar maiúsculas de minúsculas.</param>
/// <param name="UnitId">Só quem tem vínculo com esta unidade.</param>
/// <param name="Status">Quem mora (vínculo em andamento) ou quem já saiu; o padrão é todos.</param>
public sealed record ResidentFilter(string? Search, Guid? UnitId, ResidentStatusFilter Status = ResidentStatusFilter.All);
