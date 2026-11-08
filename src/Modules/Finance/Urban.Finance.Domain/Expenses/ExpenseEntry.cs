using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Expenses;

/// <summary>A despesa em um mês. Editar o valor aqui muda só este mês; a regra da despesa continua a mesma.</summary>
public sealed class ExpenseEntry
{
    private ExpenseEntry(Expense expense, YearMonth month, decimal? amount, string? label, DateOnly dueDate, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = expense.CondominiumId;
        ExpenseId = expense.Id;
        GroupId = expense.GroupId;
        Month = month;
        Description = expense.Description;
        Amount = amount;
        Label = label;
        DueDate = dueDate;
        Status = EntryStatus.Pending;
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

    /// <summary>Vencimento deste mês, calculado quando o lançamento foi gerado. Mudar o dia na despesa não altera lançamentos já gerados.</summary>
    public DateOnly DueDate { get; }

    /// <summary>Situação guardada: Pending, Paid ou Canceled. Para saber se venceu, use <see cref="StatusOn"/>.</summary>
    public EntryStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Verdadeiro enquanto o valor de uma despesa variável não foi informado.</summary>
    public bool NeedsAmount => Amount is null;

    internal static ExpenseEntry Create(Expense expense, YearMonth month, decimal? amount, string? label, DateOnly dueDate, DateTimeOffset now) =>
        new(expense, month, amount, label, dueDate, now);

    /// <summary>Situação em uma data: pendente vira vencido só depois do dia do vencimento.</summary>
    public EntryStatus StatusOn(DateOnly today) =>
        Status == EntryStatus.Pending && DueDate < today ? EntryStatus.Overdue : Status;

    /// <summary>Marca como pendente, pago ou cancelado. Pago exige o valor; vencido não se marca, é calculado.</summary>
    public Result SetStatus(EntryStatus status)
    {
        if (status == EntryStatus.Overdue || !Enum.IsDefined(status))
        {
            return Result.Failure(ExpenseErrors.StatusInvalid);
        }

        if (status == EntryStatus.Paid && NeedsAmount)
        {
            return Result.Failure(ExpenseErrors.AmountRequiredToPay);
        }

        Status = status;
        return Result.Success();
    }

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
