namespace Urban.Finance.Domain.Expenses;

/// <summary>
/// Situação de pagamento de um lançamento. Só Pending, Paid e Canceled são guardados: Overdue é calculado
/// pela data de vencimento (veja <see cref="ExpenseEntry.StatusOn"/>).
/// </summary>
public enum EntryStatus
{
    Pending,
    Paid,
    Overdue,
    Canceled,
}
