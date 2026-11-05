namespace Urban.Condominium.Domain.Units;

/// <summary>
/// O que identifica uma unidade dentro do condomínio: bloco (opcional) mais número. A comparação ignora maiúsculas e
/// minúsculas e "sem bloco" é o mesmo que bloco vazio. O banco garante a mesma regra com um índice único.
/// </summary>
public readonly struct UnitIdentity : IEquatable<UnitIdentity>
{
    public UnitIdentity(string? block, string number)
    {
        Block = block ?? string.Empty;
        Number = number;
    }

    /// <summary>Vazio quando a unidade não tem bloco.</summary>
    public string Block { get; }

    public string Number { get; }

    public bool Equals(UnitIdentity other) =>
        string.Equals(Block, other.Block, StringComparison.OrdinalIgnoreCase) && string.Equals(Number, other.Number, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is UnitIdentity other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Block), StringComparer.OrdinalIgnoreCase.GetHashCode(Number));

    public static bool operator ==(UnitIdentity left, UnitIdentity right) => left.Equals(right);

    public static bool operator !=(UnitIdentity left, UnitIdentity right) => !left.Equals(right);
}
