namespace Urban.Condominium.Domain.Units;

/// <summary>Os dados de uma unidade já validados e normalizados, prontos para gravar ou para comparar com o que está gravado.</summary>
public sealed record UnitData(string? Block, string Number, UnitType Type, decimal? IdealFraction, decimal? Area, IReadOnlyList<string> ParkingSpots)
{
    public UnitData(string? block, string number, UnitType type, decimal? idealFraction, decimal? area)
        : this(block, number, type, idealFraction, area, [])
    {
    }

    // A lista de vagas se compara pelo conteúdo e na ordem: o record compararia só a referência.
    public bool Equals(UnitData? other) =>
        other is not null
        && Block == other.Block
        && Number == other.Number
        && Type == other.Type
        && IdealFraction == other.IdealFraction
        && Area == other.Area
        && ParkingSpots.SequenceEqual(other.ParkingSpots);

    public override int GetHashCode() => HashCode.Combine(Block, Number, Type, IdealFraction, Area, ParkingSpots.Count);
}
