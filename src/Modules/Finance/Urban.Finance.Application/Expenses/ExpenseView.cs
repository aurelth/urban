using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Expenses;

public sealed record InstallmentView(int Number, string Month, decimal Amount);

public sealed record AmountChangeView(string FromMonth, decimal Amount);

/// <param name="CurrentAmount">O valor em vigor no mês atual (ou o último, se a despesa já acabou, ou o primeiro, se ainda não começou). Nulo nas variáveis.</param>
/// <param name="IsEnded">Se o último mês da despesa já passou.</param>
public sealed record ExpenseView(
    Guid Id,
    Guid GroupId,
    string Description,
    string? Supplier,
    ExpenseKind Kind,
    string StartMonth,
    string? EndMonth,
    string? LastMonth,
    decimal? CurrentAmount,
    int DueDay,
    int? InstallmentCount,
    int? CurrentInstallment,
    IReadOnlyList<InstallmentView> Installments,
    IReadOnlyList<AmountChangeView> AmountChanges,
    bool IsEnded,
    string Version)
{
    public static ExpenseView From(ExpenseRecord record, YearMonth reference) => From(record.Expense, record.Version, reference);

    public static ExpenseView From(Expense expense, string version, YearMonth reference)
    {
        YearMonth shown = Clamp(reference, expense.StartMonth, expense.LastMonth);

        return new ExpenseView(
            expense.Id,
            expense.GroupId,
            expense.Description,
            expense.Supplier,
            expense.Kind,
            expense.StartMonth.ToString(),
            expense.EndMonth?.ToString(),
            expense.LastMonth?.ToString(),
            expense.AmountFor(shown),
            expense.DueDay,
            expense.InstallmentCount,
            expense.CurrentInstallment,
            [.. expense.Installments.OrderBy(parcel => parcel.Number).Select(parcel => new InstallmentView(parcel.Number, MonthOf(expense, parcel.Number).ToString(), parcel.Amount))],
            [.. expense.AmountChanges.OrderBy(change => change.FromMonth).Select(change => new AmountChangeView(change.FromMonth.ToString(), change.Amount))],
            expense.IsEnded(reference),
            version);
    }

    private static YearMonth MonthOf(Expense expense, int number) => expense.StartMonth.AddMonths(number - expense.CurrentInstallment!.Value);

    private static YearMonth Clamp(YearMonth month, YearMonth first, YearMonth? last)
    {
        if (month < first)
        {
            return first;
        }

        return last is { } end && month > end ? end : month;
    }
}
