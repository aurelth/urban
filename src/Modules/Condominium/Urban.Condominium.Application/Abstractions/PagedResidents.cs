namespace Urban.Condominium.Application.Abstractions;

/// <summary>Uma página de moradores, já em ordem de nome, e quantos o filtro encontrou ao todo.</summary>
public sealed record PagedResidents(IReadOnlyList<ResidentRecord> Items, int Total);
