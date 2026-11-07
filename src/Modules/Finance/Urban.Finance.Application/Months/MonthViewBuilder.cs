using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Months;

internal static class MonthViewBuilder
{
    /// <summary>Monta o mês: lançamentos por grupo e descrição, o total de cada grupo e quantos ainda faltam preencher.</summary>
    public static MonthView Build(
        YearMonth month,
        IReadOnlyList<ExpenseEntryRecord> current,
        IReadOnlyList<ExpenseEntryRecord> previous,
        IReadOnlyList<ExpenseGroupRecord> groups)
    {
        Dictionary<Guid, string> groupNames = groups.ToDictionary(record => record.Group.Id, record => record.Group.Name);
        Dictionary<Guid, decimal?> previousAmounts = previous.ToDictionary(record => record.Entry.ExpenseId, record => record.Entry.Amount);

        List<ExpenseEntryView> entries =
        [
            .. current
                .OrderBy(record => NameOf(groupNames, record.Entry.GroupId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Entry.Description, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Entry.Id)
                .Select(record => ExpenseEntryView.From(record, previousAmounts.GetValueOrDefault(record.Entry.ExpenseId))),
        ];

        List<GroupTotalView> totals =
        [
            .. entries
                .GroupBy(entry => entry.GroupId)
                .Select(group => new GroupTotalView(
                    group.Key,
                    NameOf(groupNames, group.Key),
                    group.Sum(entry => entry.Amount ?? 0m),
                    group.Count(entry => entry.IsPending)))
                .OrderBy(group => group.GroupName, StringComparer.OrdinalIgnoreCase),
        ];

        return new MonthView(month.ToString(), entries, totals, totals.Sum(group => group.Total), totals.Sum(group => group.PendingCount));
    }

    private static string NameOf(Dictionary<Guid, string> names, Guid groupId) => names.GetValueOrDefault(groupId, string.Empty);
}
