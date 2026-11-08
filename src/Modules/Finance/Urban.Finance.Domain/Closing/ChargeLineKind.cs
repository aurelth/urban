namespace Urban.Finance.Domain.Closing;

/// <summary>De onde vem cada linha do recibo da unidade.</summary>
public enum ChargeLineKind
{
    Expenses,
    ReserveFund,
    Water,
    Sewage,
    Gas,
}
