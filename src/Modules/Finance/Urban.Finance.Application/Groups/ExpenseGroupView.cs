using Urban.Finance.Application.Abstractions;

namespace Urban.Finance.Application.Groups;

public sealed record ExpenseGroupView(Guid Id, string Name, string Version)
{
    public static ExpenseGroupView From(ExpenseGroupRecord record) => new(record.Group.Id, record.Group.Name, record.Version);
}
