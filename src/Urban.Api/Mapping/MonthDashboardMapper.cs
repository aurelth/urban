using System.Globalization;
using Urban.Api.Endpoints;
using Urban.Finance.Application.Dashboard;

namespace Urban.Api.Mapping;

/// <summary>Do resultado da consulta para a resposta. Datas em AAAA-MM-DD.</summary>
public static class MonthDashboardMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static MonthDashboardResponse ToResponse(MonthDashboardView view) => new(
        view.Month,
        Format(view.AsOf),
        view.Charges is not null,
        new DashboardExpensesResponse(
            view.Expenses.Total,
            view.Expenses.NeedsAmountCount,
            view.Expenses.OverdueCount,
            [.. view.Expenses.Groups.Select(group => new GroupTotalResponse(group.GroupId, group.GroupName, group.Total, group.NeedsAmountCount))]),
        view.Charges is null
            ? null
            : new DashboardChargesResponse(
                Format(view.Charges.DueDate),
                view.Charges.Charged,
                view.Charges.Received,
                view.Charges.Balance,
                view.Charges.Units,
                view.Charges.Open,
                view.Charges.Partial,
                view.Charges.Paid,
                view.Charges.Overdue,
                view.Charges.OverdueAmount));

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
