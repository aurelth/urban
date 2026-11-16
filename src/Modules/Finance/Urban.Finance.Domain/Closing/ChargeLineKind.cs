namespace Urban.Finance.Domain.Closing;

/// <summary>De onde vem cada linha da fatura da unidade.</summary>
public enum ChargeLineKind
{
    Expenses,
    ReserveFund,
    Water,
    Sewage,
    Gas,
}
