namespace Urban.Api.Endpoints;

/// <param name="ChargeId">A cobrança; é o que a rota da fatura em PDF pede.</param>
/// <param name="DueDate">O vencimento (AAAA-MM-DD).</param>
/// <param name="Amount">O valor cobrado da unidade.</param>
/// <param name="Paid">O que já foi pago, sem os pagamentos estornados.</param>
/// <param name="Balance">O que falta pagar.</param>
/// <param name="Status">open, partial, paid ou overdue, calculada a cada leitura com o dia de hoje em Brasília.</param>
/// <param name="LastReceivedOn">O dia do último pagamento recebido (AAAA-MM-DD), ou nulo se nada foi pago.</param>
public sealed record MyChargeResponse(Guid ChargeId, string DueDate, decimal Amount, decimal Paid, decimal Balance, string Status, string? LastReceivedOn);

/// <param name="Charge">Nulo enquanto o mês não fechou, ou se a unidade não recebeu cobrança nele.</param>
public sealed record MyUnitResponse(Guid UnitId, string? Block, string Number, MyChargeResponse? Charge);

public sealed record MyExpenseGroupResponse(Guid GroupId, string GroupName, decimal Total);

/// <param name="Total">O total das despesas do mês, sem as canceladas.</param>
/// <param name="Groups">O total de cada grupo de despesa, em ordem alfabética.</param>
public sealed record MyExpensesResponse(decimal Total, IReadOnlyList<MyExpenseGroupResponse> Groups);

/// <param name="Month">AAAA-MM.</param>
/// <param name="AsOf">O dia, em Brasília, a que a situação das cobranças se refere (AAAA-MM-DD).</param>
/// <param name="Access">unit (mora numa ou mais unidades), dependent (só como dependente, sem cobrança própria) ou none (sem vínculo com unidade).</param>
/// <param name="Closed">Se o mês já foi fechado.</param>
/// <param name="Units">As unidades da conta, cada uma com a sua cobrança; vazio para dependent e none.</param>
/// <param name="Expenses">Só de mês fechado e só para quem mora em unidade; nulo nos demais casos.</param>
public sealed record MyMonthResponse(string Month, string AsOf, string Access, bool Closed, IReadOnlyList<MyUnitResponse> Units, MyExpensesResponse? Expenses);
