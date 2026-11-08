using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Expenses;

namespace Urban.Finance.Application.Months;

/// <param name="SuggestedAmount">Só nos lançamentos pendentes: o valor do mês anterior da mesma despesa, para a tela sugerir.</param>
public sealed record ExpenseEntryView(
    Guid Id,
    Guid ExpenseId,
    Guid GroupId,
    string Month,
    string Description,
    decimal? Amount,
    string? Label,
    bool IsPending,
    decimal? SuggestedAmount,
    string Version)
{
    public static ExpenseEntryView From(ExpenseEntryRecord record, decimal? suggestedAmount)
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
            entry.IsPending,
            entry.IsPending ? suggestedAmount : null,
            record.Version);
    }
}

public sealed record GroupTotalView(Guid GroupId, string GroupName, decimal Total, int PendingCount);

public sealed record MonthView(
    string Month,
    IReadOnlyList<ExpenseEntryView> Entries,
    IReadOnlyList<GroupTotalView> Groups,
    decimal Total,
    int PendingCount);
