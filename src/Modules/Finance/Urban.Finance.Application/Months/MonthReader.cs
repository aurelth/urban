using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Months;

internal static class MonthReader
{
    public static async Task<MonthView> ReadAsync(
        IExpenseEntries entries,
        IMonthClosings closings,
        IExpenseGroups groups,
        YearMonth month,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseEntryRecord> current = await entries.ListAsync(month, cancellationToken);
        IReadOnlyList<ExpenseEntryRecord> previous = await entries.ListAsync(month.AddMonths(-1), cancellationToken);
        IReadOnlyList<ExpenseGroupRecord> allGroups = await groups.ListAsync(cancellationToken);
        MonthClosing? closing = await closings.FindAsync(month, cancellationToken);

        return MonthViewBuilder.Build(month, today, current, previous, allGroups, closing);
    }
}
