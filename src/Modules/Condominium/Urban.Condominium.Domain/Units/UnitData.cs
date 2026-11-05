namespace Urban.Condominium.Domain.Units;

/// <summary>Os dados de uma unidade já validados e normalizados, prontos para gravar ou para comparar com o que está gravado.</summary>
public sealed record UnitData(string? Block, string Number, UnitType Type, decimal? IdealFraction, decimal? Area);
