namespace Urban.Finance.Domain.Expenses;

public sealed class ExpenseInstallment
{
    internal ExpenseInstallment(int number, decimal amount)
    {
        Id = Guid.CreateVersion7();
        Number = number;
        Amount = amount;
    }

    // Usado só pelo EF Core ao ler do banco.
    private ExpenseInstallment()
    {
    }

    public Guid Id { get; }

    public int Number { get; }

    public decimal Amount { get; internal set; }
}
