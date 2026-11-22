using Urban.Finance.Application.Months;

namespace Urban.Finance.Application.Dashboard;

/// <summary>As despesas do mês sem os cancelados: total, quantas esperam valor, quantas venceram e o total de cada grupo.</summary>
public sealed record DashboardExpensesView(decimal Total, int NeedsAmountCount, int OverdueCount, IReadOnlyList<GroupTotalView> Groups);

/// <summary>
/// As cobranças de um mês fechado. <paramref name="Open"/>, <paramref name="Partial"/>, <paramref name="Paid"/> e
/// <paramref name="Overdue"/> contam as unidades de cada situação e somam <paramref name="Units"/>.
/// <paramref name="OverdueAmount"/> é o saldo das cobranças atrasadas deste mês.
/// </summary>
public sealed record DashboardChargesView(
    DateOnly DueDate, decimal Charged, decimal Received, decimal Balance, int Units, int Open, int Partial, int Paid, int Overdue, decimal OverdueAmount);

/// <summary><paramref name="Charges"/> é nulo enquanto o mês não está fechado.</summary>
public sealed record MonthDashboardView(string Month, DateOnly AsOf, DashboardExpensesView Expenses, DashboardChargesView? Charges);
