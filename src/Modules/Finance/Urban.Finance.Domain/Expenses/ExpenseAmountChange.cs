using Urban.Finance.Domain.Months;

namespace Urban.Finance.Domain.Expenses;

/// <summary>Valor de uma despesa fixa a partir de um mês. O histórico é mantido: cada mudança vale "a partir de".</summary>
public sealed class ExpenseAmountChange
{
    internal ExpenseAmountChange(YearMonth fromMonth, decimal amount)
    {
        Id = Guid.CreateVersion7();
        FromMonth = fromMonth;
        Amount = amount;
    }

    // Usado só pelo EF Core ao ler do banco.
    private ExpenseAmountChange()
    {
    }

    public Guid Id { get; }

    public YearMonth FromMonth { get; }

    public decimal Amount { get; internal set; }
}
