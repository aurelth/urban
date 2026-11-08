using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Expenses;

namespace Urban.Finance.Application.Months;

/// <param name="Status">Situação em "hoje": Pending, Paid, Overdue (pendente depois do vencimento) ou Canceled.</param>
/// <param name="NeedsAmount">Verdadeiro enquanto o valor de uma despesa variável não foi informado.</param>
/// <param name="SuggestedAmount">Só nos lançamentos que precisam de valor: o valor do mês anterior da mesma despesa, para a tela sugerir.</param>
public sealed record ExpenseEntryView(
    Guid Id,
    Guid ExpenseId,
    Guid GroupId,
    string Month,
    string Description,
    decimal? Amount,
    string? Label,
    DateOnly DueDate,
    EntryStatus Status,
    bool NeedsAmount,
    decimal? SuggestedAmount,
    string Version)
{
    public static ExpenseEntryView From(ExpenseEntryRecord record, decimal? suggestedAmount, DateOnly today)
    {
        ExpenseEntry entry = record.Entry;

        return new ExpenseEntryView(
            entry.Id,
            entry.ExpenseId,
            entry.GroupId,
            entry.Month.ToString(),
            entry.Description,
            entry.Amount,
            entry.Label,
            entry.DueDate,
            entry.StatusOn(today),
            entry.NeedsAmount,
            entry.NeedsAmount ? suggestedAmount : null,
            record.Version);
    }
}

/// <param name="Total">Soma dos valores do grupo, sem os cancelados.</param>
/// <param name="NeedsAmountCount">Quantos lançamentos do grupo (não cancelados) ainda esperam o valor.</param>
public sealed record GroupTotalView(Guid GroupId, string GroupName, decimal Total, int NeedsAmountCount);

/// <param name="Total">Soma dos valores do mês, sem os cancelados.</param>
/// <param name="NeedsAmountCount">Quantos lançamentos (não cancelados) ainda esperam o valor.</param>
/// <param name="OverdueCount">Quantos lançamentos estão vencidos hoje.</param>
public sealed record MonthView(
    string Month,
    IReadOnlyList<ExpenseEntryView> Entries,
    IReadOnlyList<GroupTotalView> Groups,
    decimal Total,
    int NeedsAmountCount,
    int OverdueCount);
