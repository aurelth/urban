namespace Urban.Api.Endpoints;

/// <param name="PaymentId">O pagamento; é o que a rota do recibo em PDF pede.</param>
/// <param name="Amount">O valor recebido.</param>
/// <param name="ReceivedOn">O dia do recebimento (AAAA-MM-DD).</param>
/// <param name="Method">pix, boleto, transfer, cash ou other.</param>
/// <param name="ReceiptNumber">O número do recibo, em sequência por condomínio.</param>
/// <param name="ReceiptCode">O número como se fala dele, "REC-000123".</param>
/// <param name="Reversed">Se o pagamento foi estornado: ele deixa de contar no que foi pago, e o recibo sai marcado como cancelado.</param>
public sealed record MyPaymentResponse(Guid PaymentId, decimal Amount, string ReceivedOn, string Method, int ReceiptNumber, string ReceiptCode, bool Reversed);

/// <param name="ChargeId">A cobrança; é o que a rota da fatura em PDF pede.</param>
/// <param name="DueDate">O vencimento (AAAA-MM-DD).</param>
/// <param name="Amount">O valor cobrado da unidade.</param>
/// <param name="Paid">O que já foi pago, sem os pagamentos estornados.</param>
/// <param name="Balance">O que falta pagar.</param>
/// <param name="Status">open, partial, paid ou overdue, calculada a cada leitura com o dia de hoje em Brasília.</param>
/// <param name="LastReceivedOn">O dia do último pagamento recebido (AAAA-MM-DD), ou nulo se nada foi pago.</param>
/// <param name="Payments">Os pagamentos da cobrança, estornados inclusive, do mais antigo para o mais novo; cada um traz o recibo.</param>
public sealed record MyChargeResponse(
    Guid ChargeId, string DueDate, decimal Amount, decimal Paid, decimal Balance, string Status, string? LastReceivedOn, IReadOnlyList<MyPaymentResponse> Payments);

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

/// <param name="Access">unit (mora numa ou mais unidades), dependent (só como dependente, sem cobrança própria) ou none (sem vínculo com unidade).</param>
public sealed record MyAccessResponse(string Access);
