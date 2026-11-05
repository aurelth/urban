namespace Urban.Condominium.Application.Abstractions;

/// <summary>Uma página de unidades, já na ordem natural, e quantas unidades o filtro encontrou ao todo.</summary>
public sealed record PagedUnits(IReadOnlyList<UnitRecord> Items, int Total);
