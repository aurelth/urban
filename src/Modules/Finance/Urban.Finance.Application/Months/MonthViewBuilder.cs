using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Months;

internal static class MonthViewBuilder
{
    /// <summary>
    /// Monta o mês: lançamentos por grupo e descrição, o total de cada grupo, quantos ainda faltam preencher e quantos estão vencidos.
    /// Os cancelados aparecem na lista, mas ficam fora dos totais e das contagens.
    /// </summary>
    public static MonthView Build(
        YearMonth month,
        DateOnly today,
        IReadOnlyList<ExpenseEntryRecord> current,
        IReadOnlyList<ExpenseEntryRecord> previous,
        IReadOnlyList<ExpenseGroupRecord> groups,
        MonthClosing? closing)
    {
        Dictionary<Guid, string> groupNames = groups.ToDictionary(record => record.Group.Id, record => record.Group.Name);
        Dictionary<Guid, decimal?> previousAmounts = previous.ToDictionary(record => record.Entry.ExpenseId, record => record.Entry.Amount);

        List<ExpenseEntryView> entries =
        [
            .. current
                .OrderBy(record => NameOf(groupNames, record.Entry.GroupId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Entry.Description, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Entry.Id)
                .Select(record => ExpenseEntryView.From(record, previousAmounts.GetValueOrDefault(record.Entry.ExpenseId), today)),
        ];

        List<GroupTotalView> totals =
        [
            .. entries
                .GroupBy(entry => entry.GroupId)
                .Select(group => new GroupTotalView(
                    group.Key,
                    NameOf(groupNames, group.Key),
                    group.Where(entry => entry.Status != EntryStatus.Canceled).Sum(entry => entry.Amount ?? 0m),
                    group.Count(entry => entry.Status != EntryStatus.Canceled && entry.NeedsAmount)))
                .OrderBy(group => group.GroupName, StringComparer.OrdinalIgnoreCase),
        ];

        return new MonthView(
            month.ToString(),
            entries,
            totals,
            totals.Sum(group => group.Total),
            totals.Sum(group => group.NeedsAmountCount),
            entries.Count(entry => entry.Status == EntryStatus.Overdue),
            closing is null ? null : MonthClosingView.From(closing));
    }

    private static string NameOf(Dictionary<Guid, string> names, Guid groupId) => names.GetValueOrDefault(groupId, string.Empty);
}
