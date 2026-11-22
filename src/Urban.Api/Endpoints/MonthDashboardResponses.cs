namespace Urban.Api.Endpoints;

/// <param name="Total">O total das despesas do mês, sem as canceladas.</param>
/// <param name="NeedsAmountCount">Quantas despesas ainda esperam o valor.</param>
/// <param name="OverdueCount">Quantas despesas já venceram sem ser pagas.</param>
/// <param name="Groups">O total de cada grupo de despesa, em ordem alfabética.</param>
public sealed record DashboardExpensesResponse(decimal Total, int NeedsAmountCount, int OverdueCount, IReadOnlyList<GroupTotalResponse> Groups);

/// <param name="DueDate">O vencimento das cobranças (AAAA-MM-DD).</param>
/// <param name="Charged">O total cobrado das unidades.</param>
/// <param name="Received">O total recebido, sem os pagamentos estornados.</param>
/// <param name="Balance">O que ainda falta receber.</param>
/// <param name="Units">Quantas unidades receberam cobrança; é a soma de open, partial, paid e overdue.</param>
/// <param name="Open">Unidades em aberto: nada pago e ainda no prazo.</param>
/// <param name="Partial">Unidades com pagamento parcial e ainda no prazo.</param>
/// <param name="Paid">Unidades quitadas.</param>
/// <param name="Overdue">Unidades com saldo depois do vencimento, com ou sem pagamento parcial.</param>
/// <param name="OverdueAmount">O saldo das cobranças atrasadas deste mês.</param>
public sealed record DashboardChargesResponse(
    string DueDate, decimal Charged, decimal Received, decimal Balance, int Units, int Open, int Partial, int Paid, int Overdue, decimal OverdueAmount);

/// <param name="Month">AAAA-MM.</param>
/// <param name="AsOf">O dia, em Brasília, a que a situação das cobranças se refere (AAAA-MM-DD).</param>
/// <param name="Closed">Se o mês já foi fechado; quando não, <paramref name="Charges"/> é nulo.</param>
public sealed record MonthDashboardResponse(string Month, string AsOf, bool Closed, DashboardExpensesResponse Expenses, DashboardChargesResponse? Charges);
