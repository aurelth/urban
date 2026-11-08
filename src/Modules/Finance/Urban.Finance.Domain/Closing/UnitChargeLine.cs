namespace Urban.Finance.Domain.Closing;

/// <summary>
/// Uma linha do recibo de uma unidade (por exemplo, "Água" ou "Fundo de reserva"). O texto, a quantidade e as leituras são
/// cópias de quando o mês fechou, como no resto da cobrança.
/// </summary>
public sealed class UnitChargeLine
{
    internal UnitChargeLine(Guid chargeId, Guid condominiumId, int position, ChargeLineKind kind, string label, decimal amount, decimal? quantity = null, decimal? previousReading = null, decimal? currentReading = null)
    {
        Id = Guid.CreateVersion7();
        ChargeId = chargeId;
        CondominiumId = condominiumId;
        Position = position;
        Kind = kind;
        Label = label;
        Amount = amount;
        Quantity = quantity;
        PreviousReading = previousReading;
        CurrentReading = currentReading;
    }

    // Usado só pelo EF Core ao ler do banco.
    private UnitChargeLine()
    {
        Label = null!;
    }

    public Guid Id { get; }

    public Guid ChargeId { get; }

    public Guid CondominiumId { get; }

    public int Position { get; }

    public ChargeLineKind Kind { get; }

    public string Label { get; }

    public decimal Amount { get; }

    /// <summary>Consumo em m³ das linhas de água, esgoto e gás; nulo nas demais.</summary>
    public decimal? Quantity { get; }

    public decimal? PreviousReading { get; }

    public decimal? CurrentReading { get; }
}
