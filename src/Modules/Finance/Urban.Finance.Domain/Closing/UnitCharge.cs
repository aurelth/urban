namespace Urban.Finance.Domain.Closing;

/// <summary>
/// O que uma unidade deve em um mês fechado. Bloco, número e peso são cópias de quando o mês fechou: se a unidade mudar
/// depois, a cobrança já emitida continua como foi.
/// </summary>
public sealed class UnitCharge
{
    private readonly List<UnitChargeLine> _lines = [];

    internal UnitCharge(Guid closingId, Guid condominiumId, Guid unitId, int position, string? block, string number, decimal weight)
    {
        Id = Guid.CreateVersion7();
        ClosingId = closingId;
        CondominiumId = condominiumId;
        UnitId = unitId;
        Position = position;
        Block = block;
        Number = number;
        Weight = weight;
    }

    // Usado só pelo EF Core ao ler do banco.
    private UnitCharge()
    {
        Number = null!;
    }

    public Guid Id { get; }

    public Guid ClosingId { get; }

    public Guid CondominiumId { get; }

    public Guid UnitId { get; }

    /// <summary>Lugar da unidade na ordem natural (bloco e número) quando o mês fechou, para a lista voltar sempre na mesma ordem.</summary>
    public int Position { get; }

    public string? Block { get; }

    public string Number { get; }

    public decimal Weight { get; }

    /// <summary>A soma das linhas.</summary>
    public decimal Amount { get; private set; }

    public IReadOnlyList<UnitChargeLine> Lines => _lines;

    internal void AddLine(ChargeLineKind kind, string label, decimal amount, decimal? quantity = null, decimal? previousReading = null, decimal? currentReading = null)
    {
        _lines.Add(new UnitChargeLine(Id, CondominiumId, _lines.Count, kind, label, amount, quantity, previousReading, currentReading));
        Amount += amount;
    }
}
