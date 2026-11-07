using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Expenses;

/// <summary>A despesa em um mês. Editar o valor aqui muda só este mês; a regra da despesa continua a mesma.</summary>
public sealed class ExpenseEntry
{
    private ExpenseEntry(Expense expense, YearMonth month, decimal? amount, string? label, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = expense.CondominiumId;
        ExpenseId = expense.Id;
        GroupId = expense.GroupId;
        Month = month;
        Description = expense.Description;
        Amount = amount;
        Label = label;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private ExpenseEntry()
    {
        Description = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid ExpenseId { get; }

    public Guid GroupId { get; private set; }

    public YearMonth Month { get; }

    public string Description { get; private set; }

    /// <summary>Nulo enquanto o valor de uma despesa variável não foi informado.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>Rótulo da parcela, como "4/6". Nulo nas demais despesas.</summary>
    public string? Label { get; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsPending => Amount is null;

    internal static ExpenseEntry Create(Expense expense, YearMonth month, decimal? amount, string? label, DateTimeOffset now) =>
        new(expense, month, amount, label, now);

    public Result SetAmount(decimal amount)
    {
        if (!MoneyRules.IsValid(amount))
        {
            return Result.Failure(ExpenseErrors.AmountInvalid);
        }

        Amount = amount;
        return Result.Success();
    }
}
