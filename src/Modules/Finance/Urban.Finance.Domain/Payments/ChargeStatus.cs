namespace Urban.Finance.Domain.Payments;

/// <summary>
/// A situação de uma cobrança. Nunca é gravada: sai de <see cref="ChargeAccount.StatusOf"/> a cada leitura, a partir do que foi
/// pago e do vencimento.
/// </summary>
public enum ChargeStatus
{
    Open = 1,
    Partial = 2,
    Paid = 3,
    Overdue = 4,
}
