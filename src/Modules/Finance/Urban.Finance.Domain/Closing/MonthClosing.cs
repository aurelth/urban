using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Domain.Closing;

/// <summary>
/// O fechamento de um mês: congela o total e a cobrança de cada unidade. Enquanto existir, os lançamentos do mês não
/// mudam; reabrir é apagar o fechamento (e as cobranças) e fechar de novo.
/// </summary>
public sealed class MonthClosing
{
    private readonly List<UnitCharge> _charges = [];

    private MonthClosing(Guid condominiumId, YearMonth month, DateOnly dueDate, decimal total, ApportionmentBasisKind basisKind, Guid closedBy, DateTimeOffset closedAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        Month = month;
        DueDate = dueDate;
        Total = total;
        BasisKind = basisKind;
        ClosedBy = closedBy;
        ClosedAt = closedAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private MonthClosing()
    {
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public YearMonth Month { get; }

    /// <summary>Vencimento de todas as cobranças deste fechamento, escolhido por quem fechou.</summary>
    public DateOnly DueDate { get; }

    /// <summary>Soma dos lançamentos não cancelados na hora do fechamento.</summary>
    public decimal Total { get; }

    public ApportionmentBasisKind BasisKind { get; }

    public Guid ClosedBy { get; }

    public DateTimeOffset ClosedAt { get; }

    public IReadOnlyList<UnitCharge> Charges => _charges;

    /// <summary>Fecha o mês: confere os lançamentos, soma o total e reparte entre as unidades pelo peso de cada uma.</summary>
    public static Result<MonthClosing> Close(
        Guid condominiumId,
        YearMonth month,
        DateOnly dueDate,
        Guid closedBy,
        DateTimeOffset now,
        IReadOnlyList<ExpenseEntry> entries,
        ApportionmentBasis basis)
    {
        if (entries.Count == 0)
        {
            return Result.Failure<MonthClosing>(ClosingErrors.MonthNotOpen);
        }

        List<ExpenseEntry> counted = [.. entries.Where(entry => entry.Status != EntryStatus.Canceled)];

        if (counted.Any(entry => entry.NeedsAmount))
        {
            return Result.Failure<MonthClosing>(ClosingErrors.AmountsPending);
        }

        decimal total = counted.Sum(entry => entry.Amount ?? 0m);

        if (total <= 0)
        {
            return Result.Failure<MonthClosing>(ClosingErrors.NothingToCharge);
        }

        if (dueDate < month.FirstDay)
        {
            return Result.Failure<MonthClosing>(ClosingErrors.DueDateInvalid);
        }

        Result<IReadOnlyList<decimal>> shares = ChargeSplitter.Split(total, [.. basis.Units.Select(unit => unit.Weight)]);

        if (shares.IsFailure)
        {
            return Result.Failure<MonthClosing>(shares.Error);
        }

        MonthClosing closing = new(condominiumId, month, dueDate, total, basis.Kind, closedBy, now);

        for (int index = 0; index < basis.Units.Count; index++)
        {
            ApportionmentUnit unit = basis.Units[index];
            closing._charges.Add(new UnitCharge(closing.Id, condominiumId, unit.UnitId, index, unit.Block, unit.Number, unit.Weight, shares.Value[index]));
        }

        return Result.Success(closing);
    }
}
