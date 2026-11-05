using Urban.Condominium.Domain.Units;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>Quais unidades a lista traz. Nulo em <paramref name="Block"/>, <paramref name="Type"/> e <paramref name="Search"/> é "sem esse filtro".</summary>
/// <param name="Search">Trecho do número ou do bloco, sem diferenciar maiúsculas de minúsculas.</param>
public sealed record UnitFilter(string? Block, UnitType? Type, UnitStatusFilter Status, string? Search);
